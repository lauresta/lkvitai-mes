using Marten;
using Marten.Events.Projections;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;

namespace LKvitai.MES.Modules.Warehouse.Projections;

/// <summary>
/// ActiveHardLocks inline projection per blueprint [MITIGATION R-4].
///
/// Uses EventProjection (not MultiStreamProjection) because a single PickingStartedEvent
/// can create MULTIPLE rows (one per hard-locked line). EventProjection gives direct access
/// to IDocumentOperations for multi-document writes within the same transaction.
///
/// CRITICAL: Must be ProjectionLifecycle.Inline (same-transaction update)
/// CRITICAL: V-5 Rule B — uses only self-contained event data (no external queries)
///
/// The document type <see cref="ActiveHardLockView"/> lives in Contracts so that
/// Infrastructure can query it without referencing the Projections project.
/// </summary>
public class ActiveHardLocksProjection : EventProjection
{
    /// <summary>
    /// Insert one row per hard-locked line when picking starts.
    /// </summary>
    public void Project(PickingStartedEvent evt, IDocumentOperations ops)
    {
        foreach (var line in evt.HardLockedLines)
        {
            ops.Store(new ActiveHardLockView
            {
                Id = ActiveHardLockView.ComputeId(evt.ReservationId, line.Location, line.SKU),
                WarehouseId = line.WarehouseId,
                ReservationId = evt.ReservationId,
                Location = line.Location,
                SKU = line.SKU,
                HardLockedQty = line.HardLockedQty,
                StartedAt = evt.Timestamp
            });
        }
    }

    /// <summary>
    /// Delete all rows for a reservation when it is consumed (HARD lock released).
    /// </summary>
    public void Project(ReservationConsumedEvent evt, IDocumentOperations ops)
    {
        ops.DeleteWhere<ActiveHardLockView>(x => x.ReservationId == evt.ReservationId);
    }

    /// <summary>
    /// Delete all rows for a reservation when it is cancelled (HARD lock released).
    /// </summary>
    public void Project(ReservationCancelledEvent evt, IDocumentOperations ops)
    {
        ops.DeleteWhere<ActiveHardLockView>(x => x.ReservationId == evt.ReservationId);
    }
}
