using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Domain.Aggregates;
using Marten.Events.Aggregation;
using Marten.Events.Projections;

namespace LKvitai.MES.Projections;

/// <summary>
/// Reservation summary projection used for paginated reservation search.
/// One document per reservation stream.
/// </summary>
public class ReservationSummaryProjection : SingleStreamProjection<ReservationSummaryView>
{
    public ReservationSummaryView Create(ReservationCreatedEvent evt)
        => ReservationSummaryAggregation.Create(evt);

    public void Apply(StockAllocatedEvent evt, ReservationSummaryView view)
        => ReservationSummaryAggregation.ApplyAllocated(evt, view);

    public void Apply(PickingStartedEvent evt, ReservationSummaryView view)
        => ReservationSummaryAggregation.ApplyPickingStarted(evt, view);

    public void Apply(ReservationConsumedEvent evt, ReservationSummaryView view)
        => ReservationSummaryAggregation.ApplyConsumed(evt, view);

    public void Apply(ReservationCancelledEvent evt, ReservationSummaryView view)
        => ReservationSummaryAggregation.ApplyCancelled(evt, view);

    public void Apply(ReservationBumpedEvent evt, ReservationSummaryView view)
        => ReservationSummaryAggregation.ApplyBumped(evt, view);
}

/// <summary>
/// Pure aggregation helpers for ReservationSummaryProjection unit tests.
/// </summary>
public static class ReservationSummaryAggregation
{
    public static ReservationSummaryView Create(ReservationCreatedEvent evt)
    {
        return new ReservationSummaryView
        {
            Id = Reservation.StreamIdFor(evt.ReservationId),
            ReservationId = evt.ReservationId,
            Purpose = evt.Purpose,
            Priority = evt.Priority,
            Status = ReservationStatus.PENDING.ToString(),
            LockType = ReservationLockType.SOFT.ToString(),
            CreatedAt = evt.Timestamp,
            PickingStartedAt = null,
            LineCount = evt.RequestedLines.Count
        };
    }

    public static void ApplyAllocated(StockAllocatedEvent evt, ReservationSummaryView view)
    {
        view.Status = ReservationStatus.ALLOCATED.ToString();
        view.LockType = ReservationLockType.SOFT.ToString();
    }

    public static void ApplyPickingStarted(PickingStartedEvent evt, ReservationSummaryView view)
    {
        view.Status = ReservationStatus.PICKING.ToString();
        view.LockType = ReservationLockType.HARD.ToString();
        view.PickingStartedAt = evt.Timestamp;
    }

    public static void ApplyConsumed(ReservationConsumedEvent evt, ReservationSummaryView view)
    {
        view.Status = ReservationStatus.CONSUMED.ToString();
    }

    public static void ApplyCancelled(ReservationCancelledEvent evt, ReservationSummaryView view)
    {
        view.Status = ReservationStatus.CANCELLED.ToString();
    }

    public static void ApplyBumped(ReservationBumpedEvent evt, ReservationSummaryView view)
    {
        if (evt.BumpedReservationId == view.ReservationId)
        {
            view.Status = ReservationStatus.BUMPED.ToString();
        }
    }
}
