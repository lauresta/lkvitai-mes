using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using Marten.Events.Projections;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Domain;

namespace LKvitai.MES.Projections;

/// <summary>
/// Available stock projection. Maintains quantities per (warehouseId, location, SKU).
/// </summary>
public class AvailableStockProjection : MultiStreamProjection<AvailableStockView, string>
{
    public AvailableStockProjection()
    {
        CustomGrouping(new AvailableStockGrouper());
    }

    public AvailableStockView Apply(StockMovedEvent evt, AvailableStockView current)
    {
        var isFromLocation = current.Id.EndsWith($":{evt.FromLocation}:{evt.SKU}", StringComparison.Ordinal);
        var isToLocation = current.Id.EndsWith($":{evt.ToLocation}:{evt.SKU}", StringComparison.Ordinal);

        InitializeFromId(current);

        if (isFromLocation)
        {
            current.OnHandQty -= evt.Quantity;
            current.LocationCode ??= evt.FromLocation;
        }
        else if (isToLocation)
        {
            current.OnHandQty += evt.Quantity;
            current.LocationCode ??= evt.ToLocation;
        }

        current.RecomputeAvailable();
        current.LastUpdated = evt.Timestamp;
        return current;
    }

    public AvailableStockView Apply(PickingStartedEvent evt, AvailableStockView current)
    {
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

    public AvailableStockView Apply(ReservationConsumedEvent evt, AvailableStockView current)
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

    public AvailableStockView Apply(ReservationCancelledEvent evt, AvailableStockView current)
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

    public AvailableStockView Apply(GoodsReceivedEvent evt, AvailableStockView current)
    {
        EnsureIdentity(current, evt.WarehouseId, evt.DestinationLocation, evt.SKU);
        current.ItemId = evt.ItemId;
        current.LocationCode = evt.DestinationLocation;
        current.LotNumber = evt.LotNumber;
        current.ExpiryDate = evt.ExpiryDate;
        current.BaseUoM = evt.BaseUoM;
        current.OnHandQty += evt.ReceivedQty;
        current.RecomputeAvailable();
        current.LastUpdated = evt.Timestamp;
        return current;
    }

    public AvailableStockView Apply(PickCompletedEvent evt, AvailableStockView current)
    {
        var isFromLocation = current.Id == AvailableStockView.ComputeId(evt.WarehouseId, evt.FromLocation, evt.SKU);
        var isToLocation = current.Id == AvailableStockView.ComputeId(evt.WarehouseId, evt.ToLocation, evt.SKU);

        EnsureIdentity(current, current.WarehouseId, current.Location, evt.SKU);
        current.ItemId = evt.ItemId;
        current.LotNumber = evt.LotNumber;

        if (isFromLocation)
        {
            current.OnHandQty -= evt.PickedQty;
            current.LocationCode = evt.FromLocation;
        }

        if (isToLocation)
        {
            current.OnHandQty += evt.PickedQty;
            current.LocationCode = evt.ToLocation;
        }

        current.RecomputeAvailable();
        current.LastUpdated = evt.Timestamp;
        return current;
    }

    public AvailableStockView Apply(StockAdjustedEvent evt, AvailableStockView current)
    {
        EnsureIdentity(current, evt.WarehouseId, evt.Location, evt.SKU);
        current.ItemId = evt.ItemId;
        current.LocationCode = evt.Location;
        current.LotNumber = evt.LotNumber;
        current.OnHandQty += evt.QtyDelta;
        current.RecomputeAvailable();
        current.LastUpdated = evt.Timestamp;
        return current;
    }

    public AvailableStockView Apply(ReservationCreatedMasterDataEvent evt, AvailableStockView current)
    {
        EnsureIdentity(current, evt.WarehouseId, evt.Location, evt.SKU);
        current.ItemId = evt.ItemId;
        current.LocationCode = evt.Location;
        current.LotNumber = evt.LotNumber;
        current.ReservedQty += evt.ReservedQty;
        current.RecomputeAvailable();
        current.LastUpdated = evt.Timestamp;
        return current;
    }

    public AvailableStockView Apply(ReservationReleasedMasterDataEvent evt, AvailableStockView current)
    {
        EnsureIdentity(current, evt.WarehouseId, current.Location, evt.SKU);
        current.ItemId = evt.ItemId;
        current.ReservedQty = Math.Max(0m, current.ReservedQty - evt.ReleasedQty);
        current.RecomputeAvailable();
        current.LastUpdated = evt.Timestamp;
        return current;
    }

    public AvailableStockView Apply(QCPassedEvent evt, AvailableStockView current)
        => ApplyQcMovement(evt, current, evt.Qty, evt.FromLocation, evt.ToLocation);

    public AvailableStockView Apply(QCFailedEvent evt, AvailableStockView current)
        => ApplyQcMovement(evt, current, evt.Qty, evt.FromLocation, evt.ToLocation);

    private static AvailableStockView ApplyQcMovement(
        WarehouseOperationalEvent evt,
        AvailableStockView current,
        decimal qty,
        string fromLocation,
        string toLocation)
    {
        var sku = evt switch
        {
            QCPassedEvent passed => passed.SKU,
            QCFailedEvent failed => failed.SKU,
            _ => string.Empty
        };

        var warehouseId = evt switch
        {
            QCPassedEvent passed => passed.WarehouseId,
            QCFailedEvent failed => failed.WarehouseId,
            _ => string.Empty
        };

        var isFromLocation = current.Id == AvailableStockView.ComputeId(warehouseId, fromLocation, sku);
        var isToLocation = current.Id == AvailableStockView.ComputeId(warehouseId, toLocation, sku);

        EnsureIdentity(current, warehouseId, current.Location, sku);

        if (isFromLocation)
        {
            current.OnHandQty -= qty;
            current.Location = fromLocation;
            current.LocationCode = fromLocation;
        }

        if (isToLocation)
        {
            current.OnHandQty += qty;
            current.Location = toLocation;
            current.LocationCode = toLocation;
        }

        current.RecomputeAvailable();
        current.LastUpdated = evt.Timestamp;
        return current;
    }

    private static void InitializeFromId(AvailableStockView current)
    {
        if (!string.IsNullOrWhiteSpace(current.WarehouseId) || string.IsNullOrWhiteSpace(current.Id))
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
        current.LocationCode ??= parts[1];
    }

    private static void EnsureIdentity(AvailableStockView current, string warehouseId, string location, string sku)
    {
        InitializeFromId(current);

        if (string.IsNullOrWhiteSpace(current.WarehouseId))
        {
            current.WarehouseId = warehouseId;
        }

        if (string.IsNullOrWhiteSpace(current.Location))
        {
            current.Location = location;
            current.LocationCode ??= location;
        }

        if (string.IsNullOrWhiteSpace(current.SKU))
        {
            current.SKU = sku;
        }
    }
}

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
                case GoodsReceivedEvent goodsReceived:
                    GroupSingle(evt, goodsReceived.WarehouseId, goodsReceived.DestinationLocation, goodsReceived.SKU, grouping);
                    break;
                case PickCompletedEvent pickCompleted:
                    GroupDual(evt, pickCompleted.WarehouseId, pickCompleted.FromLocation, pickCompleted.ToLocation, pickCompleted.SKU, grouping);
                    break;
                case StockAdjustedEvent adjusted:
                    GroupSingle(evt, adjusted.WarehouseId, adjusted.Location, adjusted.SKU, grouping);
                    break;
                case ReservationCreatedMasterDataEvent reservationCreated:
                    GroupSingle(evt, reservationCreated.WarehouseId, reservationCreated.Location, reservationCreated.SKU, grouping);
                    break;
                case ReservationReleasedMasterDataEvent reservationReleased:
                    // Release events may not include location. Group by SKU across warehouse by deriving from stream only when possible.
                    if (!string.IsNullOrWhiteSpace(reservationReleased.SKU))
                    {
                        var keyPrefix = $":{reservationReleased.SKU}";
                        var candidateIds = await session.Query<AvailableStockView>()
                            .Where(x => x.WarehouseId == reservationReleased.WarehouseId && x.SKU == reservationReleased.SKU)
                            .Select(x => x.Id)
                            .ToListAsync();

                        foreach (var id in candidateIds.Where(x => x.EndsWith(keyPrefix, StringComparison.Ordinal)))
                        {
                            grouping.AddEvent(id, evt);
                        }
                    }
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
        var streamKey = evt.StreamKey
            ?? throw new InvalidOperationException("StreamKey is null for StockMovedEvent");
        var streamId = StockLedgerStreamId.Parse(streamKey);
        var warehouseId = streamId.WarehouseId;

        var fromKey = AvailableStockView.ComputeId(warehouseId, stockMoved.FromLocation, stockMoved.SKU);
        grouping.AddEvent(fromKey, evt);

        var toKey = AvailableStockView.ComputeId(warehouseId, stockMoved.ToLocation, stockMoved.SKU);
        grouping.AddEvent(toKey, evt);
    }

    private static void GroupPickingStarted(IEvent evt, PickingStartedEvent pickingStarted, ITenantSliceGroup<string> grouping)
    {
        foreach (var line in pickingStarted.HardLockedLines)
        {
            var key = AvailableStockView.ComputeId(line.WarehouseId, line.Location, line.SKU);
            grouping.AddEvent(key, evt);
        }
    }

    private static void GroupReservationConsumed(IEvent evt, ReservationConsumedEvent consumed, ITenantSliceGroup<string> grouping)
    {
        foreach (var line in consumed.ReleasedHardLockLines)
        {
            var key = AvailableStockView.ComputeId(line.WarehouseId, line.Location, line.SKU);
            grouping.AddEvent(key, evt);
        }
    }

    private static void GroupReservationCancelled(IEvent evt, ReservationCancelledEvent cancelled, ITenantSliceGroup<string> grouping)
    {
        foreach (var line in cancelled.ReleasedHardLockLines)
        {
            var key = AvailableStockView.ComputeId(line.WarehouseId, line.Location, line.SKU);
            grouping.AddEvent(key, evt);
        }
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

        var key = AvailableStockView.ComputeId(warehouseId, location, sku);
        grouping.AddEvent(key, evt);
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

public static class AvailableStockAggregation
{
    public static AvailableStockView Apply(StockMovedEvent evt, AvailableStockView current, string streamId)
    {
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
            view.LocationCode = location;
        }
    }
}
