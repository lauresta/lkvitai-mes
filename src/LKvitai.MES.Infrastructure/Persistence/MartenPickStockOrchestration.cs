using LKvitai.MES.Application.Orchestration;
using LKvitai.MES.Application.Ports;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Domain;
using LKvitai.MES.Domain.Aggregates;
using LKvitai.MES.SharedKernel;
using Marten;
using Marten.Exceptions;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Infrastructure.Persistence;

/// <summary>
/// Marten implementation of <see cref="IPickStockOrchestration"/>.
///
/// [MITIGATION V-3] Transaction ordering:
///   1. Validate reservation is PICKING (HARD locked)
///   2. Record StockMovement to PRODUCTION via StockLedger (FIRST)
///   3. Consume reservation (independent of HU projection)
///
/// [HOTFIX CRIT-01] RecordStockMovement acquires pg_advisory_xact_lock via
/// IBalanceGuardLock to serialize with StartPicking and other outbound operations.
///
/// HU projection processes StockMoved event ASYNCHRONOUSLY — NOT waited on.
///
/// If StockMovement succeeds but reservation consumption fails, the result
/// indicates "movement committed, consumption deferred" so the caller can
/// publish to the MassTransit saga for durable retry.
/// </summary>
public class MartenPickStockOrchestration : IPickStockOrchestration
{
    private readonly IDocumentStore _store;
    private readonly IBalanceGuardLockFactory _lockFactory;
    private readonly ILogger<MartenPickStockOrchestration> _logger;
    private const int MaxConcurrencyRetries = 3;

    public MartenPickStockOrchestration(
        IDocumentStore store,
        IBalanceGuardLockFactory lockFactory,
        ILogger<MartenPickStockOrchestration> logger)
    {
        _store = store;
        _lockFactory = lockFactory;
        _logger = logger;
    }

    public async Task<PickStockResult> ExecuteAsync(
        Guid reservationId,
        Guid handlingUnitId,
        string warehouseId,
        string sku,
        decimal quantity,
        string fromLocation,
        Guid operatorId,
        CancellationToken ct = default)
    {
        // ── Step 1: Validate reservation is PICKING ────────────────────
        Reservation? reservation;
        await using (var readSession = _store.LightweightSession())
        {
            var streamId = Reservation.StreamIdFor(reservationId);
            reservation = await readSession.Events.AggregateStreamAsync<Reservation>(
                streamId, token: ct);
        }

        if (reservation is null)
            return PickStockResult.MovementFailed(DomainErrorCodes.ReservationNotFound);

        if (reservation.Status != ReservationStatus.PICKING)
            return PickStockResult.MovementFailed(DomainErrorCodes.ReservationNotPicking);

        // ── Step 2: Record StockMovement FIRST (V-3) ──────────────────
        Guid movementId;
        try
        {
            movementId = await RecordStockMovementAsync(
                warehouseId, sku, quantity, fromLocation, operatorId, handlingUnitId, ct);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex,
                "PickStock StockMovement domain failure: Reservation {ReservationId}, SKU {SKU}, Code {ErrorCode}",
                reservationId, sku, ex.ErrorCode);
            return PickStockResult.MovementFailed(ex.ErrorCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "PickStock StockMovement failed: Reservation {ReservationId}, SKU {SKU}",
                reservationId, sku);
            return PickStockResult.MovementFailed(DomainErrorCodes.PickStockMovementFailed);
        }

        _logger.LogInformation(
            "PickStock movement recorded: Movement {MovementId}, Reservation {ReservationId}",
            movementId, reservationId);

        // ── Step 3: Consume reservation (NO wait for HU projection) ───
        try
        {
            var consumeResult = await ConsumeReservationAsync(reservationId, quantity, ct);

            if (consumeResult.IsSuccess)
            {
                return PickStockResult.Ok(movementId);
            }

            // Consumption failed — movement is committed, defer to saga
            _logger.LogWarning(
                "PickStock consumption failed, deferring to saga: Reservation {ReservationId}, Error: {Error}",
                reservationId, consumeResult.Error);
            return PickStockResult.ConsumptionDeferred(movementId, consumeResult.Error);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "PickStock consumption exception, deferring to saga: Reservation {ReservationId}",
                reservationId);
            return PickStockResult.ConsumptionDeferred(movementId, DomainErrorCodes.PickStockConsumptionFailed);
        }
    }

    /// <inheritdoc />
    public async Task<Result> ConsumeReservationAsync(
        Guid reservationId, decimal quantity, CancellationToken ct = default)
    {
        for (int attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
        {
            await using var session = _store.LightweightSession();

            try
            {
                var streamId = Reservation.StreamIdFor(reservationId);

                // Load reservation to get current state and hard-locked lines
                var reservation = await session.Events.AggregateStreamAsync<Reservation>(
                    streamId, token: ct);

                if (reservation is null)
                    return Result.Fail(DomainErrorCodes.ReservationNotFound);

                // Already consumed — idempotent
                if (reservation.Status == ReservationStatus.CONSUMED)
                    return Result.Ok();

                if (reservation.Status != ReservationStatus.PICKING)
                    return Result.Fail(DomainErrorCodes.ReservationNotPicking);

                // Get stream version for optimistic concurrency
                var streamState = await session.Events.FetchStreamStateAsync(streamId, ct);
                var expectedVersion = streamState?.Version ?? -1;

                // Build released hard lock lines from reservation state
                var releasedLines = reservation.Lines.Select(l => new HardLockLineDto
                {
                    WarehouseId = l.WarehouseId,
                    Location = l.Location,
                    SKU = l.SKU,
                    HardLockedQty = l.AllocatedQuantity > 0 ? l.AllocatedQuantity : l.RequestedQuantity
                }).ToList();

                // Append ReservationConsumedEvent
                var consumedEvent = new ReservationConsumedEvent
                {
                    ReservationId = reservationId,
                    ActualQuantity = quantity,
                    Timestamp = DateTime.UtcNow,
                    ReleasedHardLockLines = releasedLines
                };

                session.Events.Append(streamId, expectedVersion, consumedEvent);
                await session.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Reservation {ReservationId} consumed with quantity {Quantity} (attempt {Attempt})",
                    reservationId, quantity, attempt);

                return Result.Ok();
            }
            catch (EventStreamUnexpectedMaxEventIdException ex) when (attempt < MaxConcurrencyRetries)
            {
                _logger.LogWarning(
                    ex,
                    "Concurrency conflict on consumption attempt {Attempt}/{Max} for Reservation {ReservationId}",
                    attempt, MaxConcurrencyRetries, reservationId);

                await Task.Delay(100 * attempt, ct);
            }
            catch (EventStreamUnexpectedMaxEventIdException)
            {
                return Result.Fail(DomainErrorCodes.ConcurrencyConflict);
            }
        }

        return Result.Fail(DomainErrorCodes.PickStockConsumptionFailed);
    }

    // ── Private helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Records the StockMovement from picking location to PRODUCTION.
    /// Uses expected-version append with bounded retry.
    ///
    /// [HOTFIX CRIT-01] Acquires advisory lock for the (warehouseId, fromLocation, sku)
    /// to serialize with StartPicking and other outbound operations.
    /// </summary>
    private async Task<Guid> RecordStockMovementAsync(
        string warehouseId, string sku, decimal quantity,
        string fromLocation, Guid operatorId, Guid handlingUnitId,
        CancellationToken ct)
    {
        var movementId = Guid.NewGuid();
        var lockKeys = StockLockKey.ForLocations(new[] { (warehouseId, fromLocation, sku) });

        await using var guardLock = await _lockFactory.CreateAsync(ct);
        await guardLock.AcquireAsync(lockKeys, ct);

        try
        {
            for (int attempt = 1; attempt <= MaxConcurrencyRetries; attempt++)
            {
                await using var session = _store.LightweightSession();

                try
                {
                    var ledgerStreamId = StockLedgerStreamId.For(warehouseId, fromLocation, sku);

                    // Hydrate the StockLedger aggregate to check balance (V-2 invariant)
                    var ledger = await session.Events.AggregateStreamAsync<StockLedger>(
                        ledgerStreamId, token: ct) ?? new StockLedger();

                    // Get current stream version for optimistic concurrency
                    var streamState = await session.Events.FetchStreamStateAsync(ledgerStreamId, ct);
                    var expectedVersion = streamState?.Version ?? -1;

                    // Produce the event (validates balance invariant)
                    var stockMovedEvent = ledger.RecordMovement(
                        movementId: movementId,
                        sku: sku,
                        quantity: quantity,
                        fromLocation: fromLocation,
                        toLocation: "PRODUCTION",
                        movementType: "PICK",
                        operatorId: operatorId,
                        handlingUnitId: handlingUnitId,
                        reason: "Pick for reservation");

                    session.Events.Append(ledgerStreamId, expectedVersion, stockMovedEvent);
                    await session.SaveChangesAsync(ct);

                    // [CRIT-01] Commit lock AFTER Marten commit succeeds
                    await guardLock.CommitAsync(ct);

                    return movementId;
                }
                catch (EventStreamUnexpectedMaxEventIdException) when (attempt < MaxConcurrencyRetries)
                {
                    await Task.Delay(100 * attempt, ct);
                }
            }

            throw new DomainException(
                DomainErrorCodes.ConcurrencyConflict,
                $"Failed to record StockMovement after {MaxConcurrencyRetries} concurrency retries.");
        }
        catch
        {
            // Lock disposed without commit → rollback releases advisory locks
            throw;
        }
    }
}
