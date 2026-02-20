using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using Marten.Events.Projections;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Domain;

namespace LKvitai.MES.Projections;

/// <summary>
/// Location balance projection per (warehouseId, location, SKU).
/// </summary>
public class LocationBalanceProjection : MultiStreamProjection<LocationBalanceView, string>
{
    public LocationBalanceProjection()
    {
        CustomGrouping(new LocationBalanceGrouper());
    }

    public LocationBalanceView Apply(StockMovedEvent evt, LocationBalanceView current)
    {
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

    public LocationBalanceView Apply(GoodsReceivedEvent evt, LocationBalanceView current)
    {
        EnsureIdentity(current, evt.WarehouseId, evt.DestinationLocation, evt.SKU);
        current.Quantity += evt.ReceivedQty;
        current.LastUpdated = evt.Timestamp;
        return current;
    }

    public LocationBalanceView Apply(PickCompletedEvent evt, LocationBalanceView current)
    {
        EnsureIdentity(current, evt.WarehouseId, current.Location, evt.SKU);

        var isFromLocation = current.Id == BuildKey(evt.WarehouseId, evt.FromLocation, evt.SKU);
        var isToLocation = current.Id == BuildKey(evt.WarehouseId, evt.ToLocation, evt.SKU);

        if (isFromLocation)
        {
            current.Quantity -= evt.PickedQty;
        }

        if (isToLocation)
        {
            current.Quantity += evt.PickedQty;
        }

        current.LastUpdated = evt.Timestamp;
        return current;
    }

    public LocationBalanceView Apply(StockAdjustedEvent evt, LocationBalanceView current)
    {
        EnsureIdentity(current, evt.WarehouseId, evt.Location, evt.SKU);
        current.Quantity += evt.QtyDelta;
        current.LastUpdated = evt.Timestamp;
        return current;
    }

    public LocationBalanceView Apply(QCPassedEvent evt, LocationBalanceView current)
        => ApplyQc(evt, current, evt.Qty, evt.FromLocation, evt.ToLocation);

    public LocationBalanceView Apply(QCFailedEvent evt, LocationBalanceView current)
        => ApplyQc(evt, current, evt.Qty, evt.FromLocation, evt.ToLocation);

    private static LocationBalanceView ApplyQc(
        WarehouseOperationalEvent evt,
        LocationBalanceView current,
        decimal qty,
        string fromLocation,
        string toLocation)
    {
        var warehouseId = evt switch
        {
            QCPassedEvent passed => passed.WarehouseId,
            QCFailedEvent failed => failed.WarehouseId,
            _ => string.Empty
        };

        var sku = evt switch
        {
            QCPassedEvent passed => passed.SKU,
            QCFailedEvent failed => failed.SKU,
            _ => string.Empty
        };

        EnsureIdentity(current, warehouseId, current.Location, sku);

        if (current.Id == BuildKey(warehouseId, fromLocation, sku))
        {
            current.Quantity -= qty;
        }

        if (current.Id == BuildKey(warehouseId, toLocation, sku))
        {
            current.Quantity += qty;
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

    private static void EnsureIdentity(LocationBalanceView current, string warehouseId, string location, string sku)
    {
        InitializeIdentityFields(current);

        if (string.IsNullOrWhiteSpace(current.WarehouseId))
        {
            current.WarehouseId = warehouseId;
        }

        if (string.IsNullOrWhiteSpace(current.Location))
        {
            current.Location = location;
        }

        if (string.IsNullOrWhiteSpace(current.SKU))
        {
            current.SKU = sku;
        }
    }

    private static string BuildKey(string warehouseId, string location, string sku)
        => $"{warehouseId}:{location}:{sku}";
}

public class LocationBalanceGrouper : IAggregateGrouper<string>
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
                case GoodsReceivedEvent goodsReceived:
                    GroupSingle(evt, goodsReceived.WarehouseId, goodsReceived.DestinationLocation, goodsReceived.SKU, grouping);
                    break;
                case PickCompletedEvent pickCompleted:
                    GroupDual(evt, pickCompleted.WarehouseId, pickCompleted.FromLocation, pickCompleted.ToLocation, pickCompleted.SKU, grouping);
                    break;
                case StockAdjustedEvent adjusted:
                    GroupSingle(evt, adjusted.WarehouseId, adjusted.Location, adjusted.SKU, grouping);
                    break;
                case QCPassedEvent qcPassed:
                    GroupDual(evt, qcPassed.WarehouseId, qcPassed.FromLocation, qcPassed.ToLocation, qcPassed.SKU, grouping);
                    break;
                case QCFailedEvent qcFailed:
                    GroupDual(evt, qcFailed.WarehouseId, qcFailed.FromLocation, qcFailed.ToLocation, qcFailed.SKU, grouping);
                    break;
            }
        }

        await Task.CompletedTask;
    }

    private static void GroupStockMoved(IEvent evt, StockMovedEvent stockMoved, ITenantSliceGroup<string> grouping)
    {
        var streamKey = evt.StreamKey ?? throw new InvalidOperationException("StreamKey is null");
        var streamId = StockLedgerStreamId.Parse(streamKey);
        var warehouseId = streamId.WarehouseId;

        var fromKey = $"{warehouseId}:{stockMoved.FromLocation}:{stockMoved.SKU}";
        grouping.AddEvent(fromKey, evt);

        var toKey = $"{warehouseId}:{stockMoved.ToLocation}:{stockMoved.SKU}";
        grouping.AddEvent(toKey, evt);
    }

    private static void GroupSingle(
        IEvent evt,
        string warehouseId,
        string location,
        string sku,
        ITenantSliceGroup<string> grouping)
    {
        if (string.IsNullOrWhiteSpace(warehouseId) ||
            string.IsNullOrWhiteSpace(location) ||
            string.IsNullOrWhiteSpace(sku))
        {
            return;
        }

        grouping.AddEvent($"{warehouseId}:{location}:{sku}", evt);
    }

    private static void GroupDual(
        IEvent evt,
        string warehouseId,
        string fromLocation,
        string toLocation,
        string sku,
        ITenantSliceGroup<string> grouping)
    {
        GroupSingle(evt, warehouseId, fromLocation, sku, grouping);
        GroupSingle(evt, warehouseId, toLocation, sku, grouping);
    }
}

public static class LocationBalanceAggregation
{
    public static LocationBalanceView Apply(StockMovedEvent evt, LocationBalanceView current, string streamId)
    {
        var parsedStreamId = StockLedgerStreamId.Parse(streamId);

        var isFromLocation = current.Id.EndsWith($":{evt.FromLocation}:{evt.SKU}");
        var isToLocation = current.Id.EndsWith($":{evt.ToLocation}:{evt.SKU}");

        if (isFromLocation)
        {
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
