using LKvitai.MES.Modules.Warehouse.Application.Orchestration;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Marten;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using Marten.Exceptions;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;

/// <summary>
/// Marten implementation of <see cref="IAllocateReservationOrchestration"/>.
///
/// Queries AvailableStock projection for stock matching reservation lines,
/// then appends StockAllocatedEvent (SOFT lock) to the reservation stream.
///
/// Uses expected-version append + bounded retry for concurrency safety.
/// </summary>
public class MartenAllocateReservationOrchestration : IAllocateReservationOrchestration
{
    private readonly IDocumentStore _store;
    private readonly WarehouseDbContext? _db;
    private readonly ILogger<MartenAllocateReservationOrchestration> _logger;
    private const int MaxAttempts = 3;

    public MartenAllocateReservationOrchestration(
        IDocumentStore store,
        ILogger<MartenAllocateReservationOrchestration> logger,
        WarehouseDbContext? db = null)
    {
        _store = store;
        _db = db;
        _logger = logger;
    }

    public async Task<Result> AllocateAsync(
        Guid reservationId, string warehouseId, CancellationToken ct = default)
    {
        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            await using var session = _db?.Database.CurrentTransaction is { } transaction
                ? _store.LightweightSession(Marten.Services.SessionOptions.ForTransaction((NpgsqlTransaction)transaction.GetDbTransaction(), false))
                : _store.LightweightSession();

            try
            {
                // ── Step 1: Load reservation ───────────────────────────────────
                var streamId = Reservation.StreamIdFor(reservationId);
                var reservation = await session.Events.AggregateStreamAsync<Reservation>(
                    streamId, token: ct);

                if (reservation is null)
                {
                    return Result.Fail(DomainErrorCodes.ReservationNotFound);
                }

                if (reservation.Status != ReservationStatus.PENDING)
                {
                    return Result.Fail(DomainErrorCodes.ReservationNotPending);
                }

                if (reservation.Purpose == SalesReservationBalance.Purpose)
                    return await AllocateSalesAsync(session, reservation, warehouseId, ct);

                // ── Step 2: Query AvailableStock for each line ─────────────────
                var allocations = new List<AllocationDto>();
                var insufficientLines = new List<string>();

                foreach (var line in reservation.Lines)
                {
                    // Find available stock for this SKU across all locations in the warehouse
                    var availableStocks = await session.Query<AvailableStockView>()
                        .Where(a => a.WarehouseId == warehouseId && a.SKU == line.SKU && a.AvailableQty > 0)
                        .OrderByDescending(a => a.AvailableQty) // prefer locations with more stock
                        .ToListAsync(ct);

                    decimal needed = line.RequestedQuantity;
                    var lineAllocation = new AllocationDto
                    {
                        SKU = line.SKU,
                        Quantity = 0m,
                        Location = string.Empty,
                        WarehouseId = warehouseId,
                        HandlingUnitIds = new List<Guid>()
                    };

                    foreach (var stock in availableStocks)
                    {
                        if (needed <= 0m) break;

                        var take = Math.Min(needed, stock.AvailableQty);
                        lineAllocation.Quantity += take;
                        lineAllocation.Location = stock.Location; // simplified: use last location
                        needed -= take;
                    }

                    if (lineAllocation.Quantity < line.RequestedQuantity)
                    {
                        insufficientLines.Add(
                            $"SKU '{line.SKU}': available={lineAllocation.Quantity}, " +
                            $"requested={line.RequestedQuantity}");
                    }
                    else
                    {
                        allocations.Add(lineAllocation);
                    }
                }

                if (insufficientLines.Count > 0)
                {
                    _logger.LogWarning(
                        "Allocation failed for Reservation {ReservationId}: insufficient stock for {Lines}",
                        reservationId, string.Join("; ", insufficientLines));
                    return Result.Fail(DomainErrorCodes.InsufficientAvailableStock);
                }

                // ── Step 3: Get stream version for optimistic concurrency ──────
                var streamState = await session.Events.FetchStreamStateAsync(streamId, ct);
                // Marten stream versions are 0 for new streams, then 1..N.
                var expectedVersion = streamState?.Version ?? 0;

                // ── Step 4: Append StockAllocatedEvent (SOFT lock) ─────────────
                var allocatedEvent = new StockAllocatedEvent
                {
                    ReservationId = reservationId,
                    Allocations = allocations,
                    LockType = "SOFT",
                    Timestamp = DateTime.UtcNow
                };

                session.Events.Append(streamId, expectedVersion + 1, allocatedEvent);
                await session.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Allocation succeeded for Reservation {ReservationId}: {LineCount} line(s) allocated (attempt {Attempt})",
                    reservationId, allocations.Count, attempt);

                return Result.Ok();
            }
            catch (EventStreamUnexpectedMaxEventIdException ex) when (attempt < MaxAttempts)
            {
                _logger.LogWarning(
                    ex,
                    "Concurrency conflict on attempt {Attempt}/{MaxAttempts} for Allocation {ReservationId}. Retrying.",
                    attempt, MaxAttempts, reservationId);

                await Task.Delay(100 * attempt, ct);
            }
            catch (EventStreamUnexpectedMaxEventIdException)
            {
                _logger.LogError(
                    "Concurrency conflict on final attempt for Allocation {ReservationId}",
                    reservationId);
                return Result.Fail(DomainErrorCodes.ConcurrencyConflict);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Allocation failed for Reservation {ReservationId}", reservationId);
                return Result.Fail(DomainErrorCodes.AllocationFailed);
            }
        }

        return Result.Fail(DomainErrorCodes.AllocationFailed);
    }
    private async Task<Result> AllocateSalesAsync(IDocumentSession session, Reservation reservation, string warehouseId, CancellationToken ct)
    {
        if (_db?.Database.CurrentTransaction is not { } transaction)
            throw new InvalidOperationException("Sales allocation requires the order transaction.");
        var npgTransaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        // Serialize competing sales reservations per SKU until their transaction commits.
        foreach (var sku in reservation.Lines.Select(x => x.SKU.ToUpperInvariant()).Distinct().OrderBy(x => x))
        {
            await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))", npgTransaction.Connection, npgTransaction);
            command.Parameters.AddWithValue("key", $"warehouse-sales:{warehouseId}:{sku}");
            await command.ExecuteNonQueryAsync(ct);
        }
        var locations = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(_db.Locations, ct);
        var physical = locations.Where(PhysicalLocation.IsPhysical).Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existing = (await CommittedEventData.ReadAsync<StockAllocatedEvent>(session, null, ct))
            .Where(x => x.SalesOrderReservation).ToList();
        var outstanding = new List<AllocationDto>();
        foreach (var id in existing.Select(x => x.ReservationId).Distinct())
            outstanding.AddRange(await SalesReservationBalance.LoadAsync(session, id, ct));
        var allocations = new List<AllocationDto>();
        foreach (var line in reservation.Lines)
        {
            var stocks = await session.Query<AvailableStockView>().Where(x => x.WarehouseId == warehouseId && x.SKU == line.SKU)
                .ToListAsync(ct);
            var needed = line.RequestedQuantity;
            foreach (var stock in stocks.Where(x => physical.Contains(x.Location)).OrderBy(x => x.Location, StringComparer.Ordinal))
            {
                var reserved = outstanding.Concat(allocations).Where(x => x.WarehouseId == warehouseId && x.Location == stock.Location &&
                    string.Equals(x.SKU, line.SKU, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Quantity);
                var available = Math.Max(0, stock.OnHandQty - stock.HardLockedQty - stock.ReservedQty - reserved);
                var take = Math.Min(needed, available);
                if (take <= 0) continue;
                allocations.Add(new AllocationDto { SKU = line.SKU, WarehouseId = warehouseId, Location = stock.Location, Quantity = take });
                needed -= take;
                if (needed == 0) break;
            }
            if (needed > 0) return Result.Fail(DomainErrorCodes.InsufficientAvailableStock);
        }
        var stream = Reservation.StreamIdFor(reservation.ReservationId);
        var state = await session.Events.FetchStreamStateAsync(stream, ct);
        session.Events.Append(stream, state!.Version + 1, new StockAllocatedEvent
        {
            ReservationId = reservation.ReservationId, SalesOrderReservation = true, LockType = "SOFT", Allocations = allocations
        });
        await session.SaveChangesAsync(ct);
        return Result.Ok();
    }

}
