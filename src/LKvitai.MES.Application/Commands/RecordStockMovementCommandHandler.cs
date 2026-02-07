using LKvitai.MES.Application.Ports;
using LKvitai.MES.Domain;
using LKvitai.MES.SharedKernel;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Application.Commands;

/// <summary>
/// Handles <see cref="RecordStockMovementCommand"/> with expected-version append (V-2)
/// and bounded retries (max 3 attempts, exponential backoff).
///
/// Stream partitioning (ADR-001):
///   - Inbound (RECEIPT, ADJUSTMENT_IN)  → stream scoped to (warehouseId, toLocation, sku)
///   - Outbound/Transfer (DISPATCH, TRANSFER, ADJUSTMENT_OUT) → stream scoped to (warehouseId, fromLocation, sku)
///
/// Workflow:
///   1. Compute stream ID from command fields
///   2. Load StockLedger aggregate + stream version
///   3. Validate invariants via aggregate (domain exception → fail immediately)
///   4. Append event with expected-version check
///   5. On ConcurrencyException → reload and retry (bounded)
/// </summary>
public class RecordStockMovementCommandHandler : IRequestHandler<RecordStockMovementCommand, Result>
{
    /// <summary>Maximum number of retry attempts for concurrency conflicts.</summary>
    public const int MaxRetries = 3;

    private readonly IStockLedgerRepository _repository;
    private readonly ILogger<RecordStockMovementCommandHandler> _logger;

    public RecordStockMovementCommandHandler(
        IStockLedgerRepository repository,
        ILogger<RecordStockMovementCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Result> Handle(RecordStockMovementCommand request, CancellationToken cancellationToken)
    {
        var movementId = Guid.NewGuid();

        // Compute the primary stream ID per ADR-001: (warehouseId, location, sku)
        // Inbound → TO-side stream; Outbound/Transfer → FROM-side stream (V-2 balance check)
        var streamId = ComputeStreamId(request);

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

                return Result.Ok();
            }
            catch (DomainException ex)
            {
                // Domain rule violation — do NOT retry
                _logger.LogWarning(
                    "StockMovement {MovementId} rejected: {Error}",
                    movementId, ex.Message);
                return Result.Fail(ex.Message);
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
                return Result.Fail($"Concurrency conflict after {MaxRetries} attempts: {ex.Message}");
            }
        }

        // Should never reach here, but satisfy the compiler
        return Result.Fail($"Unexpected: exceeded retry loop ({MaxRetries} attempts)");
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
