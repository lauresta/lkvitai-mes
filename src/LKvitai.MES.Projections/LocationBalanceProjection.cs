using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using Marten.Events.Projections;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Domain;

namespace LKvitai.MES.Projections;

/// <summary>
/// LocationBalance projection per blueprint
/// Subscribes to StockMoved events and maintains current balance per (warehouseId, location, SKU)
/// CRITICAL: Must be MultiStreamProjection (flat table across all StockLedger streams)
/// CRITICAL: Must be Async lifecycle (uses Marten async daemon)
/// CRITICAL: V-5 Rule B - Uses only self-contained event data (no external queries)
/// 
/// Marten MultiStreamProjection works by:
/// 1. Each StockMoved event affects 2 balances (FROM and TO locations)
/// 2. We use CustomGrouping to extract warehouseId from stream ID
/// 3. Marten creates/updates separate documents for each identity
/// </summary>
public class LocationBalanceProjection : MultiStreamProjection<LocationBalanceView, string>
{
    public LocationBalanceProjection()
    {
        // CustomGrouping allows us to extract warehouseId from stream ID
        // and create identities for both FROM and TO locations
        CustomGrouping(new LocationBalanceGrouper());
    }

    public LocationBalanceView Apply(StockMovedEvent evt, LocationBalanceView current)
    {
        // Marten sets the aggregate identity to current.Id for multi-stream projections.
        // Use it to detect whether this slice represents FROM or TO location.
        var isFromLocation = current.Id.EndsWith($":{evt.FromLocation}:{evt.SKU}", StringComparison.Ordinal);
        var isToLocation = current.Id.EndsWith($":{evt.ToLocation}:{evt.SKU}", StringComparison.Ordinal);

        if (string.IsNullOrEmpty(current.WarehouseId))
        {
            InitializeIdentityFields(current);
        }

        if (isFromLocation)
        {
            current.Quantity -= evt.Quantity;
        }

        if (isToLocation)
        {
            current.Quantity += evt.Quantity;
        }

        current.LastUpdated = evt.Timestamp;
        return current;
    }

    private static void InitializeIdentityFields(LocationBalanceView current)
    {
        if (string.IsNullOrWhiteSpace(current.Id))
        {
            return;
        }

        var parts = current.Id.Split(':', 3, StringSplitOptions.None);
        if (parts.Length != 3)
        {
            return;
        }

        current.WarehouseId = parts[0];
        current.Location = parts[1];
        current.SKU = parts[2];
    }
}

/// <summary>
/// Custom grouper for LocationBalance projection
/// Extracts warehouseId from stream ID and creates identities for FROM and TO locations
/// </summary>
public class LocationBalanceGrouper : IAggregateGrouper<string>
{
    public async Task Group(IQuerySession session, IEnumerable<IEvent> events, ITenantSliceGroup<string> grouping)
    {
        var stockMovedEvents = events
            .OfType<IEvent<StockMovedEvent>>()
            .ToList();
        
        foreach (var evt in stockMovedEvents)
        {
            // Extract warehouseId from the StreamKey (string-based stream ID)
            // StreamKey is used because MartenConfiguration sets StreamIdentity.AsString
            var streamKey = evt.StreamKey ?? throw new InvalidOperationException("StreamKey is null");
            var streamId = StockLedgerStreamId.Parse(streamKey);
            var warehouseId = streamId.WarehouseId;
            
            // Create identity for FROM location (balance decreases)
            var fromKey = $"{warehouseId}:{evt.Data.FromLocation}:{evt.Data.SKU}";
            grouping.AddEvent(fromKey, evt);
            
            // Create identity for TO location (balance increases)
            var toKey = $"{warehouseId}:{evt.Data.ToLocation}:{evt.Data.SKU}";
            grouping.AddEvent(toKey, evt);
        }
        
        await Task.CompletedTask;
    }
}

/// <summary>
/// Aggregation logic for LocationBalance
/// V-5 Rule B: Uses only self-contained event data (no external queries)
/// NOTE: For testing purposes, this class provides standalone Apply logic
/// that mimics the projection behavior
/// </summary>
public static class LocationBalanceAggregation
{
    public static LocationBalanceView Apply(StockMovedEvent evt, LocationBalanceView current, string streamId)
    {
        // Extract warehouseId from stream ID
        var parsedStreamId = StockLedgerStreamId.Parse(streamId);
        
        // Determine if this is FROM or TO location based on identity
        var isFromLocation = current.Id.EndsWith($":{evt.FromLocation}:{evt.SKU}");
        var isToLocation = current.Id.EndsWith($":{evt.ToLocation}:{evt.SKU}");
        
        if (isFromLocation)
        {
            // FROM location - decrease balance
            if (string.IsNullOrEmpty(current.WarehouseId))
            {
                current.WarehouseId = parsedStreamId.WarehouseId;
                current.Location = evt.FromLocation;
                current.SKU = evt.SKU;
            }
            current.Quantity -= evt.Quantity;
        }
        else if (isToLocation)
        {
            // TO location - increase balance
            if (string.IsNullOrEmpty(current.WarehouseId))
            {
                current.WarehouseId = parsedStreamId.WarehouseId;
                current.Location = evt.ToLocation;
                current.SKU = evt.SKU;
            }
            current.Quantity += evt.Quantity;
        }
        
        current.LastUpdated = evt.Timestamp;
        return current;
    }
}
