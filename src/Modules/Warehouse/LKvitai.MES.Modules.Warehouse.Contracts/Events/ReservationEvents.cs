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

/// <summary>
/// Event published when picking starts (SOFT → HARD lock transition)
/// CRITICAL: Must include all data needed by ActiveHardLocks inline projection (V-5 Rule B compliance)
/// </summary>
public class PickingStartedEvent : DomainEvent
{
    public Guid ReservationId { get; set; }
    public string LockType { get; set; } = "HARD";
    
    /// <summary>
    /// Lines being hard-locked (location, SKU, quantity)
    /// Required for ActiveHardLocks inline projection to avoid querying Reservation state
    /// </summary>
    public List<HardLockLineDto> HardLockedLines { get; set; } = new();
}

public class ReservationConsumedEvent : DomainEvent
{
    public Guid ReservationId { get; set; }
    public decimal ActualQuantity { get; set; }

    /// <summary>
    /// Lines released from HARD lock when reservation is consumed.
    /// Required for AvailableStock projection (V-5 Rule B compliance).
    /// Empty list for reservations that were never HARD-locked.
    /// </summary>
    public List<HardLockLineDto> ReleasedHardLockLines { get; set; } = new();
}

public class ReservationCancelledEvent : DomainEvent
{
    public Guid ReservationId { get; set; }
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Lines released from HARD lock when reservation is cancelled.
    /// Required for AvailableStock projection (V-5 Rule B compliance).
    /// Empty list for reservations that were never HARD-locked.
    /// </summary>
    public List<HardLockLineDto> ReleasedHardLockLines { get; set; } = new();
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
    public string Location { get; set; } = string.Empty;
    public string WarehouseId { get; set; } = string.Empty;
    public List<Guid> HandlingUnitIds { get; set; } = new();
}

/// <summary>
/// Hard lock line data for ActiveHardLocks projection
/// Self-contained event data (V-5 Rule B compliance)
/// </summary>
public class HardLockLineDto
{
    public string WarehouseId { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public decimal HardLockedQty { get; set; }
}
