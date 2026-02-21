using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Domain;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Modules.Warehouse.Application.Commands;

/// <summary>
/// Handles <see cref="RecordStockMovementCommand"/> with expected-version append (V-2)
/// and bounded retries (max 3 attempts, exponential backoff).
///
/// Stream partitioning (ADR-001):
///   - Inbound (RECEIPT, ADJUSTMENT_IN)  → stream scoped to (warehouseId, toLocation, sku)
///   - Outbound/Transfer (DISPATCH, TRANSFER, ADJUSTMENT_OUT, PICK) → stream scoped to (warehouseId, fromLocation, sku)
///
/// [HOTFIX CRIT-01] Balance-decreasing movements acquire pg_advisory_xact_lock via
/// IBalanceGuardLock to serialize with StartPicking and other outbound operations.
/// This prevents hardLockedSum > balance invariant violations from concurrent operations.
///
/// Workflow:
///   1. Compute stream ID from command fields
///   2. If balance-decreasing: acquire advisory lock for (warehouseId, fromLocation, sku)
///   3. Load StockLedger aggregate + stream version
///   4. Validate invariants via aggregate (domain exception → fail immediately)
///   5. Append event with expected-version check
///   6. On ConcurrencyException → reload and retry (bounded)
///   7. Release advisory lock (Marten commits BEFORE lock released)
/// </summary>
public class RecordStockMovementCommandHandler : IRequestHandler<RecordStockMovementCommand, Result>
{
    /// <summary>Maximum number of retry attempts for concurrency conflicts.</summary>
    public const int MaxRetries = 3;

    private readonly IStockLedgerRepository _repository;
    private readonly IBalanceGuardLockFactory _lockFactory;
    private readonly ILogger<RecordStockMovementCommandHandler> _logger;

    public RecordStockMovementCommandHandler(
        IStockLedgerRepository repository,
        IBalanceGuardLockFactory lockFactory,
        ILogger<RecordStockMovementCommandHandler> logger)
    {
        _repository = repository;
        _lockFactory = lockFactory;
        _logger = logger;
    }

    public async Task<Result> Handle(RecordStockMovementCommand request, CancellationToken cancellationToken)
    {
        var movementId = Guid.NewGuid();

        // Compute the primary stream ID per ADR-001: (warehouseId, location, sku)
        // Inbound → TO-side stream; Outbound/Transfer → FROM-side stream (V-2 balance check)
        var streamId = ComputeStreamId(request);

        // [HOTFIX CRIT-01] Acquire advisory lock for balance-decreasing movements.
        // This serializes with StartPicking and other outbound operations on the same (location, sku).
        var needsLock = MovementType.IsBalanceDecreasing(request.MovementType);
        IBalanceGuardLock? guardLock = null;

        try
        {
            if (needsLock)
            {
                guardLock = await _lockFactory.CreateAsync(cancellationToken);
                var lockKeys = StockLockKey.ForLocations(new[]
                {
                    (request.WarehouseId, request.FromLocation, request.SKU)
                });
                await guardLock.AcquireAsync(lockKeys, cancellationToken);
            }

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                // Step 1: Load current aggregate state + stream version
                var (ledger, version) = await _repository.LoadAsync(streamId, cancellationToken);

                // Step 2: Validate domain invariants and produce event
                // Domain exceptions (e.g., insufficient balance) break out immediately — no retry.
                try
                {
                    var evt = ledger.RecordMovement(
                        movementId,
                        request.SKU,
                        request.Quantity,
                        request.FromLocation,
                        request.ToLocation,
                        request.MovementType,
                        request.OperatorId,
                        request.HandlingUnitId,
                        request.Reason);

                    // Step 3: Append with expected-version (V-2)
                    await _repository.AppendEventAsync(
                        streamId, evt, version, cancellationToken);

                    _logger.LogInformation(
                        "StockMovement {MovementId} recorded on stream {StreamId}: {Qty} x {SKU} from '{From}' to '{To}' (attempt {Attempt})",
                        movementId, streamId, request.Quantity, request.SKU,
                        request.FromLocation, request.ToLocation, attempt);

                    // [CRIT-01] Commit advisory lock AFTER Marten commit succeeds.
                    // Next serialized session sees all committed data (READ COMMITTED).
                    if (guardLock is not null)
                        await guardLock.CommitAsync(cancellationToken);

                    return Result.Ok();
                }
                catch (InsufficientBalanceException ex)
                {
                    // Domain rule violation — do NOT retry
                    _logger.LogWarning(
                        "StockMovement {MovementId} rejected: {Error}",
                        movementId, ex.Message);
                    var detail = $"Warehouse '{request.WarehouseId}': {ex.Message}";
                    return Result.Fail(DomainErrorCodes.InsufficientBalance, detail);
                }
                catch (DomainException ex)
                {
                    _logger.LogWarning(
                        "StockMovement {MovementId} rejected: {Error}",
                        movementId, ex.Message);
                    return Result.Fail(ex.ErrorCode, ex.Message);
                }
                catch (ConcurrencyException ex) when (attempt < MaxRetries)
                {
                    // Concurrency conflict — retry with exponential backoff
                    var delay = TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 100);
                    _logger.LogWarning(
                        ex,
                        "Concurrency conflict on attempt {Attempt}/{MaxRetries} for movement {MovementId} on stream {StreamId}. Retrying in {Delay}ms",
                        attempt, MaxRetries, movementId, streamId, delay.TotalMilliseconds);

                    await Task.Delay(delay, cancellationToken);
                }
                catch (ConcurrencyException ex)
                {
                    // Final attempt also failed — give up
                    _logger.LogError(
                        ex,
                        "Concurrency conflict on final attempt {Attempt}/{MaxRetries} for movement {MovementId} on stream {StreamId}",
                        attempt, MaxRetries, movementId, streamId);
                    return Result.Fail(
                        DomainErrorCodes.ConcurrencyConflict,
                        $"Concurrency conflict after {MaxRetries} attempts: {ex.Message}");
                }
            }

            // Should never reach here, but satisfy the compiler
            return Result.Fail(
                DomainErrorCodes.InternalError,
                $"Unexpected: exceeded retry loop ({MaxRetries} attempts)");
        }
        finally
        {
            // [CRIT-01] Dispose releases the lock (rollback if not committed).
            if (guardLock is not null)
                await guardLock.DisposeAsync();
        }
    }

    /// <summary>
    /// Computes the StockLedger stream ID from command fields per ADR-001.
    /// Inbound movements target the TO location's stream.
    /// Outbound/transfer movements target the FROM location's stream (V-2 balance check).
    /// </summary>
    private static string ComputeStreamId(RecordStockMovementCommand request)
    {
        if (MovementType.IsInbound(request.MovementType))
            return StockLedgerStreamId.For(request.WarehouseId, request.ToLocation, request.SKU);

        return StockLedgerStreamId.For(request.WarehouseId, request.FromLocation, request.SKU);
    }
}
