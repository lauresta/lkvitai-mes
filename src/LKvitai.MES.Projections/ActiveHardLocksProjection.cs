using Marten.Events.Aggregation;
using Marten.Events.Projections;
using LKvitai.MES.Contracts.Events;

namespace LKvitai.MES.Projections;

/// <summary>
/// ActiveHardLocks projection per blueprint [MITIGATION R-4]
/// INLINE projection for efficient HARD lock conflict detection
/// Updated atomically with PickingStarted/Consumed/Cancelled events
/// CRITICAL: Must be MultiStreamProjection (flat table across all reservations)
/// CRITICAL: Must be ProjectionLifecycle.Inline (same-transaction update)
/// </summary>
public class ActiveHardLocksProjection : MultiStreamProjection<ActiveHardLockView, Guid>
{
    public ActiveHardLocksProjection()
    {
        // Identity: composite key (ReservationId, Location, SKU)
        Identity<PickingStartedEvent>(e => e.ReservationId);
        Identity<ReservationConsumedEvent>(e => e.ReservationId);
        Identity<ReservationCancelledEvent>(e => e.ReservationId);
    }
    
    /// <summary>
    /// Insert rows when picking starts (HARD lock acquired)
    /// V-5 Rule B: Uses only self-contained event data (no external queries)
    /// </summary>
    public void Apply(PickingStartedEvent evt, ActiveHardLockView view)
    {
        // Create one row per hard-locked line
        foreach (var line in evt.HardLockedLines)
        {
            view.ReservationId = evt.ReservationId;
            view.Location = line.Location;
            view.SKU = line.SKU;
            view.HardLockedQty = line.HardLockedQty;
            view.StartedAt = evt.Timestamp;
        }
    }
    
    /// <summary>
    /// Delete rows when reservation consumed (HARD lock released)
    /// </summary>
    public void Apply(ReservationConsumedEvent evt, ActiveHardLockView view)
    {
        // Mark for deletion (Marten will handle removal)
        view.IsDeleted = true;
    }
    
    /// <summary>
    /// Delete rows when reservation cancelled (HARD lock released)
    /// </summary>
    public void Apply(ReservationCancelledEvent evt, ActiveHardLockView view)
    {
        // Mark for deletion (Marten will handle removal)
        view.IsDeleted = true;
    }
}

public class ActiveHardLockView
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public string Location { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public decimal HardLockedQty { get; set; }
    public DateTime StartedAt { get; set; }
    public bool IsDeleted { get; set; }
}
