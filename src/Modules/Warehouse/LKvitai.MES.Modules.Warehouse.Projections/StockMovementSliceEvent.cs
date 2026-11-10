using Marten.Events;
using Marten.Events.Aggregation;
using Marten.Events.Projections;

namespace LKvitai.MES.Modules.Warehouse.Projections;

/// <summary>
/// A stock movement contributes a different signed quantity to each location slice.
/// Keep the persisted event intact and give the async projections an unambiguous
/// event for each slice, including when that slice has no document yet.
/// </summary>
public sealed record StockMovementSliceEvent(
    string WarehouseId,
    string Location,
    string SKU,
    decimal QuantityDelta,
    DateTime OccurredAt,
    int? ItemId = null,
    string? LotNumber = null);

internal static class StockMovementSliceGrouping
{
    public static void Group(IEvent source, string warehouseId, string fromLocation,
        string toLocation, string sku, decimal quantity, DateTime timestamp,
        ITenantSliceGroup<string> grouping, Func<string, string, string, string> keyFor,
        int? itemId = null, string? lotNumber = null)
    {
        Add(fromLocation, -quantity);
        Add(toLocation, quantity);

        void Add(string location, decimal delta)
        {
            grouping.AddEvent(keyFor(warehouseId, location, sku),
                new ProjectedStockMovementEvent(source,
                    new StockMovementSliceEvent(warehouseId, location, sku, delta, timestamp, itemId, lotNumber)));
        }
    }
}

public sealed class ProjectedStockMovementEvent : IEvent<StockMovementSliceEvent>
{
    private readonly IEvent _source;

    public ProjectedStockMovementEvent(IEvent source, StockMovementSliceEvent data)
    {
        _source = source;
        Data = data;
    }

    public StockMovementSliceEvent Data { get; }
    object IEvent.Data => Data;
    public Type EventType => typeof(StockMovementSliceEvent);
    public Guid Id { get => _source.Id; set => _source.Id = value; }
    public long Version { get => _source.Version; set => _source.Version = value; }
    public long Sequence { get => _source.Sequence; set => _source.Sequence = value; }
    public Guid StreamId { get => _source.StreamId; set => _source.StreamId = value; }
    public string? StreamKey { get => _source.StreamKey; set => _source.StreamKey = value; }
    public DateTimeOffset Timestamp { get => _source.Timestamp; set => _source.Timestamp = value; }
    public string TenantId { get => _source.TenantId; set => _source.TenantId = value; }
    public string EventTypeName { get => _source.EventTypeName; set => _source.EventTypeName = value; }
    public string DotNetTypeName { get => _source.DotNetTypeName; set => _source.DotNetTypeName = value; }
    public string? CausationId { get => _source.CausationId; set => _source.CausationId = value; }
    public string? CorrelationId { get => _source.CorrelationId; set => _source.CorrelationId = value; }
    public Dictionary<string, object>? Headers { get => _source.Headers; set => _source.Headers = value; }
    public bool IsArchived { get => _source.IsArchived; set => _source.IsArchived = value; }
    public string? AggregateTypeName { get => _source.AggregateTypeName; set => _source.AggregateTypeName = value; }
    public void SetHeader(string key, object value) => _source.SetHeader(key, value);
    public object? GetHeader(string key) => _source.GetHeader(key);
}
