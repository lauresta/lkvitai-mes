using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Domain.Aggregates;

/// <summary>
/// Reservation aggregate - Event sourced, manages hybrid locking (SOFT → HARD)
/// </summary>
public class Reservation
{
    // Aggregate placeholder - business logic to be implemented
    // Per blueprint: Uses Marten expected-version append for HARD lock acquisition
    // Stream ID: reservation-{reservationId}
    
    public Guid ReservationId { get; private set; }
    public ReservationStatus Status { get; private set; }
    public ReservationLockType LockType { get; private set; }
    public int Priority { get; private set; }
    public List<ReservationLine> Lines { get; private set; } = new();
    
    // Event sourcing: Apply methods to be implemented
}

public enum ReservationStatus
{
    PENDING,
    ALLOCATED,
    PICKING,
    CONSUMED,
    CANCELLED,
    BUMPED
}

public enum ReservationLockType
{
    SOFT,
    HARD
}

public class ReservationLine
{
    public string SKU { get; set; } = string.Empty;
    public decimal RequestedQuantity { get; set; }
    public List<Guid> AllocatedHUs { get; set; } = new();
}
