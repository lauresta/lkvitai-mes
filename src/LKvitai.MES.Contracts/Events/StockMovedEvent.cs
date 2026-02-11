using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Contracts.Events;

/// <summary>
/// Legacy stock movement event schema (v1).
/// Retained for upcasting support only.
/// </summary>
public class StockMovedV1Event : DomainEvent
{
    public Guid MovementId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public Guid OperatorId { get; set; }
    public Guid? HandlingUnitId { get; set; }
    public string? Reason { get; set; }
}

/// <summary>
/// Event published when stock is moved between locations
/// </summary>
public class StockMovedEvent : DomainEvent
{
    public Guid MovementId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string FromLocation { get; set; } = string.Empty;
    public string ToLocation { get; set; } = string.Empty;
    public string MovementType { get; set; } = string.Empty;
    public Guid OperatorId { get; set; }
    public Guid? HandlingUnitId { get; set; }
    public string? Reason { get; set; }
}
