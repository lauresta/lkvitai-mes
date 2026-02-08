using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using Marten.Events.Projections;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Domain;

namespace LKvitai.MES.Projections;

/// <summary>
/// HandlingUnit projection per design doc (Requirement 6.9, 2.7).
/// Maintains HU state per HU ID from:
///   - HU lifecycle events (Created, LineAdded, LineRemoved, Sealed) → status/metadata
///   - StockMoved events (with HandlingUnitId) → lines and location updates
///
/// Design spec projection logic for StockMoved:
///   If fromLocation == HU.location: RemoveLine(sku, qty)
///   If toLocation == HU.location: AddLine(sku, qty)
///   If locations differ: Update HU.location
///
/// Sealed invariant (defensive): once Status == "SEALED", LineAdded / LineRemoved /
/// StockMoved are silently ignored. Primary enforcement is in the command path;
/// the projection guard prevents stale or replayed events from corrupting state.
///
/// CRITICAL: Async lifecycle (uses Marten async daemon)
/// CRITICAL: V-5 Rule B — uses only self-contained event data (no external queries)
/// </summary>
public class HandlingUnitProjection : MultiStreamProjection<HandlingUnitView, string>
{
    public HandlingUnitProjection()
    {
        CustomGrouping(new HandlingUnitGrouper());
    }

    // ── HandlingUnitCreatedEvent → initialize the HU view ───────────

    public HandlingUnitView Apply(HandlingUnitCreatedEvent evt, HandlingUnitView current)
    {
        current.HuId = evt.HuId;
        current.LPN = evt.LPN;
        current.Type = evt.Type;
        current.Status = "OPEN";
        current.WarehouseId = evt.WarehouseId;
        current.CurrentLocation = evt.Location;
        current.CreatedAt = evt.Timestamp;
        current.LastUpdated = evt.Timestamp;
        return current;
    }

    // ── LineAddedToHandlingUnitEvent → add line (split/merge/adjustment) ──

    public HandlingUnitView Apply(LineAddedToHandlingUnitEvent evt, HandlingUnitView current)
    {
        // Sealed HU is immutable — ignore post-seal mutations (defensive)
        if (current.Status == "SEALED") return current;

        current.AddLine(evt.SKU, evt.Quantity);
        current.LastUpdated = evt.Timestamp;
        return current;
    }

    // ── LineRemovedFromHandlingUnitEvent → remove line (split/merge) ──

    public HandlingUnitView Apply(LineRemovedFromHandlingUnitEvent evt, HandlingUnitView current)
    {
        // Sealed HU is immutable — ignore post-seal mutations (defensive)
        if (current.Status == "SEALED") return current;

        current.RemoveLine(evt.SKU, evt.Quantity);
        current.LastUpdated = evt.Timestamp;
        return current;
    }

    // ── HandlingUnitSealedEvent → seal the HU ───────────────────────

    public HandlingUnitView Apply(HandlingUnitSealedEvent evt, HandlingUnitView current)
    {
        current.Status = "SEALED";
        current.SealedAt = evt.SealedAt;
        current.LastUpdated = evt.Timestamp;
        return current;
    }

    // ── StockMovedEvent → update lines/location per design spec ─────

    public HandlingUnitView Apply(StockMovedEvent evt, HandlingUnitView current)
    {
        // Sealed HU is immutable — ignore post-seal mutations (defensive)
        if (current.Status == "SEALED") return current;

        // Design spec projection logic:
        //   If fromLocation == HU.location: RemoveLine(sku, qty)
        //   If toLocation == HU.location: AddLine(sku, qty)
        //   If locations differ: Update HU.location

        var huLocation = current.CurrentLocation;

        if (!string.IsNullOrEmpty(huLocation))
        {
            // Normal case: HU location is established
            if (string.Equals(evt.FromLocation, huLocation, StringComparison.Ordinal))
            {
                current.RemoveLine(evt.SKU, evt.Quantity);
            }

            if (string.Equals(evt.ToLocation, huLocation, StringComparison.Ordinal))
            {
                current.AddLine(evt.SKU, evt.Quantity);
            }

            // If movement is from HU's location to a different location, update location
            if (string.Equals(evt.FromLocation, huLocation, StringComparison.Ordinal)
                && !string.Equals(evt.ToLocation, huLocation, StringComparison.Ordinal))
            {
                current.CurrentLocation = evt.ToLocation;
            }
        }
        else
        {
            // Edge case: HU location not yet established (StockMoved arrived
            // before HandlingUnitCreated in async processing).
            // For receipt from virtual location (SUPPLIER, SYSTEM, etc.),
            // treat toLocation as the HU's location and add the line.
            if (IsVirtualLocation(evt.FromLocation))
            {
                current.CurrentLocation = evt.ToLocation;
                current.AddLine(evt.SKU, evt.Quantity);
            }
        }

        // Update status to EMPTY if all lines removed
        if (current.IsEmpty && current.Status != "OPEN" && current.Lines.Count == 0)
        {
            current.Status = "EMPTY";
        }

        current.LastUpdated = evt.Timestamp;
        return current;
    }

    private static bool IsVirtualLocation(string location)
    {
        return location is "SUPPLIER" or "PRODUCTION" or "SCRAP" or "SYSTEM";
    }
}

/// <summary>
/// Custom grouper for HandlingUnit projection.
/// Routes events from heterogeneous streams to HandlingUnitView documents keyed by HU ID.
///
/// - HU lifecycle events: keyed by HuId from the event
/// - StockMoved events: keyed by HandlingUnitId (if present)
/// </summary>
public class HandlingUnitGrouper : IAggregateGrouper<string>
{
    public async Task Group(IQuerySession session, IEnumerable<IEvent> events, ITenantSliceGroup<string> grouping)
    {
        foreach (var evt in events)
        {
            switch (evt.Data)
            {
                case HandlingUnitCreatedEvent created:
                    grouping.AddEvent(created.HuId.ToString(), evt);
                    break;

                case LineAddedToHandlingUnitEvent lineAdded:
                    grouping.AddEvent(lineAdded.HuId.ToString(), evt);
                    break;

                case LineRemovedFromHandlingUnitEvent lineRemoved:
                    grouping.AddEvent(lineRemoved.HuId.ToString(), evt);
                    break;

                case HandlingUnitSealedEvent sealed_:
                    grouping.AddEvent(sealed_.HuId.ToString(), evt);
                    break;

                case StockMovedEvent stockMoved when stockMoved.HandlingUnitId.HasValue:
                    grouping.AddEvent(stockMoved.HandlingUnitId.Value.ToString(), evt);
                    break;

                // StockMoved events without HandlingUnitId are ignored by this projection
            }
        }

        await Task.CompletedTask;
    }
}

/// <summary>
/// Static helper exposing the same aggregation logic for unit testing
/// without Marten infrastructure. Mirrors the Apply methods on HandlingUnitProjection.
///
/// V-5 Rule B: Uses only self-contained event data (no external queries).
/// Sealed invariant: post-seal LineAdded/LineRemoved/StockMoved are ignored.
/// </summary>
public static class HandlingUnitAggregation
{
    public static HandlingUnitView ApplyCreated(HandlingUnitCreatedEvent evt, HandlingUnitView current)
    {
        current.HuId = evt.HuId;
        current.LPN = evt.LPN;
        current.Type = evt.Type;
        current.Status = "OPEN";
        current.WarehouseId = evt.WarehouseId;
        current.CurrentLocation = evt.Location;
        current.CreatedAt = evt.Timestamp;
        current.LastUpdated = evt.Timestamp;
        return current;
    }

    public static HandlingUnitView ApplyLineAdded(LineAddedToHandlingUnitEvent evt, HandlingUnitView current)
    {
        if (current.Status == "SEALED") return current;

        current.AddLine(evt.SKU, evt.Quantity);
        current.LastUpdated = evt.Timestamp;
        return current;
    }

    public static HandlingUnitView ApplyLineRemoved(LineRemovedFromHandlingUnitEvent evt, HandlingUnitView current)
    {
        if (current.Status == "SEALED") return current;

        current.RemoveLine(evt.SKU, evt.Quantity);
        current.LastUpdated = evt.Timestamp;
        return current;
    }

    public static HandlingUnitView ApplySealed(HandlingUnitSealedEvent evt, HandlingUnitView current)
    {
        current.Status = "SEALED";
        current.SealedAt = evt.SealedAt;
        current.LastUpdated = evt.Timestamp;
        return current;
    }

    public static HandlingUnitView ApplyStockMoved(StockMovedEvent evt, HandlingUnitView current)
    {
        if (current.Status == "SEALED") return current;

        var huLocation = current.CurrentLocation;

        if (!string.IsNullOrEmpty(huLocation))
        {
            if (string.Equals(evt.FromLocation, huLocation, StringComparison.Ordinal))
            {
                current.RemoveLine(evt.SKU, evt.Quantity);
            }

            if (string.Equals(evt.ToLocation, huLocation, StringComparison.Ordinal))
            {
                current.AddLine(evt.SKU, evt.Quantity);
            }

            if (string.Equals(evt.FromLocation, huLocation, StringComparison.Ordinal)
                && !string.Equals(evt.ToLocation, huLocation, StringComparison.Ordinal))
            {
                current.CurrentLocation = evt.ToLocation;
            }
        }
        else
        {
            if (IsVirtualLocation(evt.FromLocation))
            {
                current.CurrentLocation = evt.ToLocation;
                current.AddLine(evt.SKU, evt.Quantity);
            }
        }

        if (current.IsEmpty && current.Status != "OPEN" && current.Lines.Count == 0)
        {
            current.Status = "EMPTY";
        }

        current.LastUpdated = evt.Timestamp;
        return current;
    }

    private static bool IsVirtualLocation(string location)
    {
        return location is "SUPPLIER" or "PRODUCTION" or "SCRAP" or "SYSTEM";
    }
}
