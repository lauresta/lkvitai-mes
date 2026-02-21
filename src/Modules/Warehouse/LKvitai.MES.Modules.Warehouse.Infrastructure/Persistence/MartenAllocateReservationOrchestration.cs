using LKvitai.MES.Modules.Warehouse.Application.Orchestration;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Marten;
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
    private readonly ILogger<MartenAllocateReservationOrchestration> _logger;
    private const int MaxAttempts = 3;

    public MartenAllocateReservationOrchestration(
        IDocumentStore store,
        ILogger<MartenAllocateReservationOrchestration> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<Result> AllocateAsync(
        Guid reservationId, string warehouseId, CancellationToken ct = default)
    {
        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            await using var session = _store.LightweightSession();

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
                // Marten uses -2 for new streams (V-2 versioning scheme)
                var expectedVersion = streamState?.Version ?? -2;

                // ── Step 4: Append StockAllocatedEvent (SOFT lock) ─────────────
                var allocatedEvent = new StockAllocatedEvent
                {
                    ReservationId = reservationId,
                    Allocations = allocations,
                    LockType = "SOFT",
                    Timestamp = DateTime.UtcNow
                };

                session.Events.Append(streamId, expectedVersion, allocatedEvent);
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
}
