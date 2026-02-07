using Marten.Events.Aggregation;

namespace LKvitai.MES.Projections;

/// <summary>
/// ActiveHardLocks projection per blueprint [MITIGATION R-4]
/// Inline projection for efficient HARD lock conflict detection
/// Updated atomically with PickingStarted/Consumed/Cancelled events
/// </summary>
public class ActiveHardLocksProjection : SingleStreamProjection<ActiveHardLockView>
{
    // Projection placeholder - implementation per blueprint to be added
    // Inline projection (same-transaction update)
    
    // Event handlers to be implemented:
    // public void Apply(PickingStartedEvent evt, ActiveHardLockView view) { }
    // public void Apply(ReservationConsumedEvent evt, ActiveHardLockView view) { }
    // public void Apply(ReservationCancelledEvent evt, ActiveHardLockView view) { }
}

public class ActiveHardLockView
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public string Location { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public decimal HardLockedQty { get; set; }
    public DateTime StartedAt { get; set; }
}
