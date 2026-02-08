using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using Marten.Events.Projections;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Domain;

namespace LKvitai.MES.Projections;

/// <summary>
/// AvailableStock projection per design doc (Requirement 6.7).
/// Maintains available stock per (warehouseId, location, SKU):
///   availableQty = max(0, onHandQty - hardLockedQty)
///
/// Subscribes to:
///   - StockMovedEvent         → updates onHandQty (same as LocationBalance)
///   - PickingStartedEvent     → increases hardLockedQty (HARD lock acquired)
///   - ReservationConsumedEvent → decreases hardLockedQty (HARD lock released)
///   - ReservationCancelledEvent → decreases hardLockedQty (HARD lock released)
///
/// CRITICAL: Async lifecycle (uses Marten async daemon)
/// CRITICAL: V-5 Rule B — uses only self-contained event data (no external queries)
/// CRITICAL: SOFT allocations do NOT reduce availability (overbooking per Req 3.3)
/// </summary>
public class AvailableStockProjection : MultiStreamProjection<AvailableStockView, string>
{
    public AvailableStockProjection()
    {
        CustomGrouping(new AvailableStockGrouper());
    }

    // ── StockMovedEvent → update onHandQty ────────────────────────────

    public AvailableStockView Apply(StockMovedEvent evt, AvailableStockView current)
    {
        // Determine if this document is the FROM or TO location
        var isFromLocation = current.Id.EndsWith($":{evt.FromLocation}:{evt.SKU}");
        var isToLocation = current.Id.EndsWith($":{evt.ToLocation}:{evt.SKU}");

        if (isFromLocation)
        {
            current.OnHandQty -= evt.Quantity;
        }
        else if (isToLocation)
        {
            current.OnHandQty += evt.Quantity;
        }

        current.RecomputeAvailable();
        current.LastUpdated = evt.Timestamp;
        return current;
    }

    // ── PickingStartedEvent → increase hardLockedQty ──────────────────

    public AvailableStockView Apply(PickingStartedEvent evt, AvailableStockView current)
    {
        // Find the matching line for this document's (warehouseId, location, sku)
        var matchingLine = evt.HardLockedLines.FirstOrDefault(l =>
            current.Id == AvailableStockView.ComputeId(l.WarehouseId, l.Location, l.SKU));

        if (matchingLine != null)
        {
            current.HardLockedQty += matchingLine.HardLockedQty;
        }

        current.RecomputeAvailable();
        current.LastUpdated = evt.Timestamp;
        return current;
    }

    // ── ReservationConsumedEvent → decrease hardLockedQty ─────────────

    public AvailableStockView Apply(ReservationConsumedEvent evt, AvailableStockView current)
    {
        // Find the matching released line for this document
        var matchingLine = evt.ReleasedHardLockLines.FirstOrDefault(l =>
            current.Id == AvailableStockView.ComputeId(l.WarehouseId, l.Location, l.SKU));

        if (matchingLine != null)
        {
            current.HardLockedQty = Math.Max(0m, current.HardLockedQty - matchingLine.HardLockedQty);
        }

        current.RecomputeAvailable();
        current.LastUpdated = evt.Timestamp;
        return current;
    }

    // ── ReservationCancelledEvent → decrease hardLockedQty ────────────

    public AvailableStockView Apply(ReservationCancelledEvent evt, AvailableStockView current)
    {
        // Find the matching released line for this document
        var matchingLine = evt.ReleasedHardLockLines.FirstOrDefault(l =>
            current.Id == AvailableStockView.ComputeId(l.WarehouseId, l.Location, l.SKU));

        if (matchingLine != null)
        {
            current.HardLockedQty = Math.Max(0m, current.HardLockedQty - matchingLine.HardLockedQty);
        }

        current.RecomputeAvailable();
        current.LastUpdated = evt.Timestamp;
        return current;
    }
}

/// <summary>
/// Custom grouper for AvailableStock projection.
/// Routes events from heterogeneous streams (StockLedger + Reservation)
/// into AvailableStockView documents keyed by (warehouseId:location:sku).
/// </summary>
public class AvailableStockGrouper : IAggregateGrouper<string>
{
    public async Task Group(IQuerySession session, IEnumerable<IEvent> events, ITenantSliceGroup<string> grouping)
    {
        foreach (var evt in events)
        {
            switch (evt.Data)
            {
                case StockMovedEvent stockMoved:
                    GroupStockMoved(evt, stockMoved, grouping);
                    break;

                case PickingStartedEvent pickingStarted:
                    GroupPickingStarted(evt, pickingStarted, grouping);
                    break;

                case ReservationConsumedEvent consumed:
                    GroupReservationConsumed(evt, consumed, grouping);
                    break;

                case ReservationCancelledEvent cancelled:
                    GroupReservationCancelled(evt, cancelled, grouping);
                    break;
            }
        }

        await Task.CompletedTask;
    }

    private static void GroupStockMoved(IEvent evt, StockMovedEvent stockMoved, ITenantSliceGroup<string> grouping)
    {
        // Extract warehouseId from the stock-ledger stream key
        var streamKey = evt.StreamKey
            ?? throw new InvalidOperationException("StreamKey is null for StockMovedEvent");
        var streamId = StockLedgerStreamId.Parse(streamKey);
        var warehouseId = streamId.WarehouseId;

        // Route to FROM location document (onHand decreases)
        var fromKey = AvailableStockView.ComputeId(warehouseId, stockMoved.FromLocation, stockMoved.SKU);
        grouping.AddEvent(fromKey, evt);

        // Route to TO location document (onHand increases)
        var toKey = AvailableStockView.ComputeId(warehouseId, stockMoved.ToLocation, stockMoved.SKU);
        grouping.AddEvent(toKey, evt);
    }

    private static void GroupPickingStarted(IEvent evt, PickingStartedEvent pickingStarted, ITenantSliceGroup<string> grouping)
    {
        // Route to each hard-locked line's (warehouseId, location, sku) document
        foreach (var line in pickingStarted.HardLockedLines)
        {
            var key = AvailableStockView.ComputeId(line.WarehouseId, line.Location, line.SKU);
            grouping.AddEvent(key, evt);
        }
    }

    private static void GroupReservationConsumed(IEvent evt, ReservationConsumedEvent consumed, ITenantSliceGroup<string> grouping)
    {
        // Route to each released line's (warehouseId, location, sku) document
        foreach (var line in consumed.ReleasedHardLockLines)
        {
            var key = AvailableStockView.ComputeId(line.WarehouseId, line.Location, line.SKU);
            grouping.AddEvent(key, evt);
        }
    }

    private static void GroupReservationCancelled(IEvent evt, ReservationCancelledEvent cancelled, ITenantSliceGroup<string> grouping)
    {
        // Route to each released line's (warehouseId, location, sku) document
        foreach (var line in cancelled.ReleasedHardLockLines)
        {
            var key = AvailableStockView.ComputeId(line.WarehouseId, line.Location, line.SKU);
            grouping.AddEvent(key, evt);
        }
    }
}

/// <summary>
/// Static helper exposing the same aggregation logic for unit testing
/// without Marten infrastructure. Mirrors the Apply methods on AvailableStockProjection.
///
/// V-5 Rule B: Uses only self-contained event data (no external queries).
/// </summary>
public static class AvailableStockAggregation
{
    /// <summary>
    /// Apply StockMovedEvent: updates onHandQty for FROM (decrease) or TO (increase) location.
    /// </summary>
    public static AvailableStockView Apply(StockMovedEvent evt, AvailableStockView current, string streamId)
    {
        // Extract warehouseId to initialise the document if needed
        var parsedStreamId = StockLedgerStreamId.Parse(streamId);

        var isFromLocation = current.Id.EndsWith($":{evt.FromLocation}:{evt.SKU}");
        var isToLocation = current.Id.EndsWith($":{evt.ToLocation}:{evt.SKU}");

        if (isFromLocation)
        {
            EnsureInit(current, parsedStreamId.WarehouseId, evt.FromLocation, evt.SKU);
            current.OnHandQty -= evt.Quantity;
        }
        else if (isToLocation)
        {
            EnsureInit(current, parsedStreamId.WarehouseId, evt.ToLocation, evt.SKU);
            current.OnHandQty += evt.Quantity;
        }

        current.RecomputeAvailable();
        current.LastUpdated = evt.Timestamp;
        return current;
    }

    /// <summary>
    /// Apply PickingStartedEvent: increases hardLockedQty for the matching line.
    /// </summary>
    public static AvailableStockView ApplyPickingStarted(PickingStartedEvent evt, AvailableStockView current)
    {
        var matchingLine = evt.HardLockedLines.FirstOrDefault(l =>
            current.Id == AvailableStockView.ComputeId(l.WarehouseId, l.Location, l.SKU));

        if (matchingLine != null)
        {
            EnsureInit(current, matchingLine.WarehouseId, matchingLine.Location, matchingLine.SKU);
            current.HardLockedQty += matchingLine.HardLockedQty;
        }

        current.RecomputeAvailable();
        current.LastUpdated = evt.Timestamp;
        return current;
    }

    /// <summary>
    /// Apply ReservationConsumedEvent: decreases hardLockedQty for the matching released line.
    /// </summary>
    public static AvailableStockView ApplyReservationConsumed(ReservationConsumedEvent evt, AvailableStockView current)
    {
        var matchingLine = evt.ReleasedHardLockLines.FirstOrDefault(l =>
            current.Id == AvailableStockView.ComputeId(l.WarehouseId, l.Location, l.SKU));

        if (matchingLine != null)
        {
            current.HardLockedQty = Math.Max(0m, current.HardLockedQty - matchingLine.HardLockedQty);
        }

        current.RecomputeAvailable();
        current.LastUpdated = evt.Timestamp;
        return current;
    }

    /// <summary>
    /// Apply ReservationCancelledEvent: decreases hardLockedQty for the matching released line.
    /// </summary>
    public static AvailableStockView ApplyReservationCancelled(ReservationCancelledEvent evt, AvailableStockView current)
    {
        var matchingLine = evt.ReleasedHardLockLines.FirstOrDefault(l =>
            current.Id == AvailableStockView.ComputeId(l.WarehouseId, l.Location, l.SKU));

        if (matchingLine != null)
        {
            current.HardLockedQty = Math.Max(0m, current.HardLockedQty - matchingLine.HardLockedQty);
        }

        current.RecomputeAvailable();
        current.LastUpdated = evt.Timestamp;
        return current;
    }

    private static void EnsureInit(AvailableStockView view, string warehouseId, string location, string sku)
    {
        if (string.IsNullOrEmpty(view.WarehouseId))
        {
            view.WarehouseId = warehouseId;
            view.Location = location;
            view.SKU = sku;
        }
    }
}
