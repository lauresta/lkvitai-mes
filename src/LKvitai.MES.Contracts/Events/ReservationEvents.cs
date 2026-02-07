using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Contracts.Events;

public class ReservationCreatedEvent : DomainEvent
{
    public Guid ReservationId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public int Priority { get; set; }
    public List<ReservationLineDto> RequestedLines { get; set; } = new();
}

public class StockAllocatedEvent : DomainEvent
{
    public Guid ReservationId { get; set; }
    public List<AllocationDto> Allocations { get; set; } = new();
    public string LockType { get; set; } = string.Empty;
}

public class PickingStartedEvent : DomainEvent
{
    public Guid ReservationId { get; set; }
    public string LockType { get; set; } = "HARD";
}

public class ReservationConsumedEvent : DomainEvent
{
    public Guid ReservationId { get; set; }
    public decimal ActualQuantity { get; set; }
}

public class ReservationCancelledEvent : DomainEvent
{
    public Guid ReservationId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class ReservationBumpedEvent : DomainEvent
{
    public Guid BumpedReservationId { get; set; }
    public Guid BumpingReservationId { get; set; }
    public List<Guid> HandlingUnitIds { get; set; } = new();
}

public class ReservationLineDto
{
    public string SKU { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
}

public class AllocationDto
{
    public string SKU { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public List<Guid> HandlingUnitIds { get; set; } = new();
}
