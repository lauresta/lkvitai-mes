using Marten.Events;

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
