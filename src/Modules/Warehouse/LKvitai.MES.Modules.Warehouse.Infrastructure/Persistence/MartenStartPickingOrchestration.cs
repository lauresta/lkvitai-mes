using LKvitai.MES.Modules.Warehouse.Application.Orchestration;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.SharedKernel;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;

/// <summary>
/// Marten implementation of <see cref="IStartPickingOrchestration"/>.
/// 
/// [MITIGATION R-3] Implements atomic HARD lock acquisition:
///   1. Load Reservation from event stream
///   2. Acquire PostgreSQL advisory locks for each (location, sku) — serializes cross-reservation conflicts
///   3. Re-read data within serialized section
///   4. Query ActiveHardLocks sum
///   5. Query StockLedger balance from event stream
///   6. Compute available = balance - hardLocked
///   7. Append PickingStartedEvent with expected-version (optimistic concurrency)
///   8. SaveChangesAsync — inline projection updates ActiveHardLocks atomically
///   9. Release advisory lock
///
/// [RISK-01] Uses pg_advisory_xact_lock on a dedicated connection for cross-reservation serialization.
///   Advisory locks avoid the phantom-row problem (no rows to lock when first reservation starts).
///   The lock serializes concurrent StartPicking targeting the same (location, sku).
///   Lock keys are sorted before acquisition to prevent deadlocks.
///   Lock released automatically when the lock connection transaction commits/rolls back.
///
/// Two-connection approach:
///   Connection A (lock): Holds advisory lock for serialization
///   Connection B (Marten): Performs reads, event append, and inline projection commit
///   Correctness: Marten commits BEFORE the advisory lock is released, ensuring the next
///   serialized session sees all committed data under READ COMMITTED isolation.
/// </summary>
public class MartenStartPickingOrchestration : IStartPickingOrchestration
{
    /// <summary>Maximum number of retry attempts for concurrency conflicts on the reservation stream.</summary>
    public const int MaxRetries = 3;

    private readonly IDocumentStore _store;
    private readonly string _connectionString;
    private readonly ILogger<MartenStartPickingOrchestration> _logger;

    public MartenStartPickingOrchestration(
        IDocumentStore store,
        IConfiguration configuration,
        ILogger<MartenStartPickingOrchestration> logger)
    {
        _store = store;
        _connectionString = configuration.GetConnectionString("WarehouseDb")
            ?? throw new InvalidOperationException(
                "WarehouseDb connection string not found in configuration.");
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> StartPickingAsync(
        Guid reservationId, Guid operatorId, CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            // Each attempt gets fresh connections (lock + Marten session).
            await using var lockConn = new NpgsqlConnection(_connectionString);
            await lockConn.OpenAsync(cancellationToken);
            await using var lockTx = await lockConn.BeginTransactionAsync(cancellationToken);

            try
            {
                // ─── Step 1: Load reservation (quick read to get lock keys) ───
                var streamId = Reservation.StreamIdFor(reservationId);
                Reservation? reservation;

                await using (var preReadSession = _store.LightweightSession())
                {
                    reservation = await preReadSession.Events.AggregateStreamAsync<Reservation>(
                        streamId, token: cancellationToken);
                }

                if (reservation is null)
                {
                    await lockTx.RollbackAsync(cancellationToken);
                    return Result.Fail(
                        DomainErrorCodes.NotFound,
                        $"Reservation {reservationId} not found.");
                }

                // Domain validation (status + lock type)
                try
                {
                    reservation.ValidateCanStartPicking();
                }
                catch (DomainException ex)
                {
                    await lockTx.RollbackAsync(cancellationToken);
                    return Result.Fail(ex.ErrorCode, ex.Message);
                }

                // ─── Step 2: Acquire advisory locks (sorted to prevent deadlocks) ───
                // [HOTFIX CRIT-01] Uses canonical StockLockKey — same key as outbound movements.
                var lockKeys = StockLockKey.ForLocations(
                    reservation.Lines.Select(l => (l.WarehouseId, l.Location, l.SKU)))
                    .ToList();

                foreach (var key in lockKeys)
                {
                    await using var cmd = new NpgsqlCommand(
                        "SELECT pg_advisory_xact_lock(@key)", lockConn);
                    cmd.Transaction = lockTx;
                    cmd.Parameters.AddWithValue("key", key);
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                // ─── Step 3: Re-read within serialized section ───
                // After advisory lock is acquired, no other StartPicking for the same
                // (location, sku) can proceed. Re-read to see latest committed data.
                await using var session = _store.LightweightSession();

                reservation = await session.Events.AggregateStreamAsync<Reservation>(
                    streamId, token: cancellationToken);

                if (reservation is null)
                {
                    await lockTx.RollbackAsync(cancellationToken);
                    return Result.Fail(
                        DomainErrorCodes.NotFound,
                        $"Reservation {reservationId} not found.");
                }

                try
                {
                    reservation.ValidateCanStartPicking();
                }
                catch (DomainException ex)
                {
                    await lockTx.RollbackAsync(cancellationToken);
                    return Result.Fail(ex.ErrorCode, ex.Message);
                }

                // ─── Step 4: Query hard locks + balances ───
                var insufficientLines = new List<string>();

                foreach (var line in reservation.Lines)
                {
                    // Query ActiveHardLocks sum for this (location, sku)
                    var sumLocked = await SumHardLockedQtyAsync(
                        session, line.Location, line.SKU, cancellationToken);

                    // Query StockLedger balance from event stream (not projection!)
                    var ledgerStreamId = StockLedgerStreamId.For(
                        line.WarehouseId, line.Location, line.SKU);
                    var ledger = await session.Events.AggregateStreamAsync<StockLedger>(
                        ledgerStreamId, token: cancellationToken) ?? new StockLedger();

                    var physicalBalance = ledger.GetBalance(line.Location, line.SKU);
                    var available = physicalBalance - sumLocked;
                    var required = line.AllocatedQuantity > 0
                        ? line.AllocatedQuantity
                        : line.RequestedQuantity;

                    if (available < required)
                    {
                        insufficientLines.Add(
                            $"SKU '{line.SKU}' at '{line.Location}': " +
                            $"physical={physicalBalance}, hardLocked={sumLocked}, " +
                            $"available={available}, required={required}");
                    }
                }

                if (insufficientLines.Count > 0)
                {
                    await lockTx.RollbackAsync(cancellationToken);
                    return Result.Fail(
                        DomainErrorCodes.HardLockConflict,
                        $"Insufficient available stock after HARD lock check: {string.Join("; ", insufficientLines)}");
                }

                // ─── Step 5: Get stream version for optimistic concurrency ───
                var streamState = await session.Events.FetchStreamStateAsync(
                    streamId, cancellationToken);
                // Marten uses -2 for new streams (V-2 versioning scheme)
                var expectedVersion = streamState?.Version ?? -2;

                // ─── Step 6: Append PickingStartedEvent ───
                var pickingStartedEvent = new PickingStartedEvent
                {
                    ReservationId = reservationId,
                    LockType = "HARD",
                    Timestamp = DateTime.UtcNow,
                    HardLockedLines = reservation.Lines.Select(l => new HardLockLineDto
                    {
                        WarehouseId = l.WarehouseId,
                        Location = l.Location,
                        SKU = l.SKU,
                        HardLockedQty = l.AllocatedQuantity > 0
                            ? l.AllocatedQuantity
                            : l.RequestedQuantity
                    }).ToList()
                };

                session.Events.Append(streamId, expectedVersion, pickingStartedEvent);

                // ─── Step 7: SaveChanges — atomic commit + inline projection ───
                // ActiveHardLocksProjection (Inline) fires here, inserting rows.
                // This commits on the Marten connection (Connection B).
                await session.SaveChangesAsync(cancellationToken);

                // ─── Step 8: Release advisory lock ───
                // Advisory lock released when lockTx commits.
                // Next serialized session will see the committed data (READ COMMITTED).
                await lockTx.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "StartPicking succeeded for reservation {ReservationId} (attempt {Attempt})",
                    reservationId, attempt);

                return Result.Ok();
            }
            catch (Marten.Exceptions.EventStreamUnexpectedMaxEventIdException ex)
                when (attempt < MaxRetries)
            {
                // Optimistic concurrency conflict on reservation stream — retry
                try { await lockTx.RollbackAsync(cancellationToken); }
                catch { /* lock connection cleanup, best-effort */ }

                var delay = TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 100);
                _logger.LogWarning(
                    ex,
                    "Concurrency conflict on attempt {Attempt}/{MaxRetries} for reservation {ReservationId}. Retrying in {Delay}ms",
                    attempt, MaxRetries, reservationId, delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
            }
            catch (Marten.Exceptions.EventStreamUnexpectedMaxEventIdException ex)
            {
                // Final attempt also failed
                try { await lockTx.RollbackAsync(cancellationToken); }
                catch { /* best-effort cleanup */ }

                _logger.LogError(
                    ex,
                    "Concurrency conflict on final attempt {Attempt}/{MaxRetries} for reservation {ReservationId}",
                    attempt, MaxRetries, reservationId);

                return Result.Fail(
                    DomainErrorCodes.ConcurrencyConflict,
                    $"Concurrency conflict after {MaxRetries} attempts for reservation {reservationId}.");
            }
            catch (Exception)
            {
                // Any other error — release lock and rethrow
                try { await lockTx.RollbackAsync(cancellationToken); }
                catch { /* best-effort cleanup */ }

                throw;
            }
        }

        return Result.Fail(
            DomainErrorCodes.InternalError,
            $"Unexpected: exceeded retry loop ({MaxRetries} attempts).");
    }

    // ----------------------------------------------------------------
    // Advisory lock helpers
    // ----------------------------------------------------------------

    /// <summary>
    /// [HOTFIX CRIT-01] Delegates to canonical StockLockKey.ForLocation.
    /// Retained for backward compatibility (e.g., existing tests that reference this method).
    /// </summary>
    public static long ComputeAdvisoryLockKey(string warehouseId, string location, string sku)
        => StockLockKey.ForLocation(warehouseId, location, sku);

    // ----------------------------------------------------------------
    // Query helpers (within same Marten session)
    // ----------------------------------------------------------------

    /// <summary>
    /// Sums hard-locked quantity for (location, sku) within the given Marten session.
    /// Queries the ActiveHardLockView documents stored by the inline projection.
    /// </summary>
    private static async Task<decimal> SumHardLockedQtyAsync(
        IQuerySession session,
        string location,
        string sku,
        CancellationToken ct)
    {
        var locks = await session.Query<ActiveHardLockView>()
            .Where(x => x.Location == location && x.SKU == sku)
            .ToListAsync(ct);

        return locks.Sum(x => x.HardLockedQty);
    }
}
