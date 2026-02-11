using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Contracts.Events;

public sealed class SalesOrderLineSnapshot
{
    public int ItemId { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }
}

public sealed class SalesOrderCreatedEvent : DomainEvent
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? RequestedDeliveryDate { get; set; }
    public List<SalesOrderLineSnapshot> Lines { get; set; } = new();
}

public sealed class SalesOrderAllocatedEvent : DomainEvent
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public DateTime AllocatedAt { get; set; }
}

public sealed class SalesOrderReleasedEvent : DomainEvent
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public DateTime ReleasedAt { get; set; }
}

public sealed class SalesOrderCancelledEvent : DomainEvent
{
    public Guid Id { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string CancelledBy { get; set; } = string.Empty;
    public DateTime CancelledAt { get; set; }
}
