using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using Marten;
using Marten.Events;
using Marten.Events.Aggregation;
using Marten.Events.Projections;

namespace LKvitai.MES.Modules.Warehouse.Projections;

/// <summary>
/// Projection that tracks active reservation rows for master-data workflows.
/// </summary>
public sealed class ActiveReservationsProjection : MultiStreamProjection<ActiveReservationView, string>
{
    public ActiveReservationsProjection()
    {
        CustomGrouping(new ActiveReservationsGrouper());
    }

    public ActiveReservationView Apply(ReservationCreatedMasterDataEvent evt, ActiveReservationView current)
    {
        current.Id = ActiveReservationView.ComputeId(evt.ReservationId);
        current.ReservationId = evt.ReservationId;
        current.OrderId = evt.OrderId;
        current.ItemId = evt.ItemId;
        current.SKU = evt.SKU;
        current.Location = evt.Location;
        current.ReservedQty += evt.ReservedQty;
        current.ExpiresAt = evt.ExpiresAt;
        current.Status = "Active";
        current.CreatedAt = current.CreatedAt == default ? evt.Timestamp : current.CreatedAt;
        current.LastUpdated = evt.Timestamp;
        return current;
    }

    public ActiveReservationView Apply(ReservationReleasedMasterDataEvent evt, ActiveReservationView current)
    {
        current.ReservedQty = Math.Max(0m, current.ReservedQty - evt.ReleasedQty);
        current.Status = string.Equals(evt.ReleaseReason, "PickCompleted", StringComparison.OrdinalIgnoreCase)
            ? "Completed"
            : "Released";
        current.LastUpdated = evt.Timestamp;
        return current;
    }

    public ActiveReservationView Apply(PickCompletedEvent evt, ActiveReservationView current)
    {
        current.Status = "Completed";
        current.ReservedQty = 0;
        current.LastUpdated = evt.Timestamp;
        return current;
    }
}

public sealed class ActiveReservationsGrouper : IAggregateGrouper<string>
{
    public async Task Group(IQuerySession session, IEnumerable<IEvent> events, ITenantSliceGroup<string> grouping)
    {
        foreach (var evt in events)
        {
            switch (evt.Data)
            {
                case ReservationCreatedMasterDataEvent created:
                    grouping.AddEvent(ActiveReservationView.ComputeId(created.ReservationId), evt);
                    break;

                case ReservationReleasedMasterDataEvent released:
                    grouping.AddEvent(ActiveReservationView.ComputeId(released.ReservationId), evt);
                    break;

                case PickCompletedEvent pickCompleted:
                    var reservationIds = await session.Query<ActiveReservationView>()
                        .Where(x => x.OrderId == pickCompleted.OrderId &&
                                    x.ItemId == pickCompleted.ItemId &&
                                    x.Status == "Active")
                        .Select(x => x.Id)
                        .ToListAsync();

                    foreach (var reservationId in reservationIds)
                    {
                        grouping.AddEvent(reservationId, evt);
                    }
                    break;
            }
        }
    }
}
