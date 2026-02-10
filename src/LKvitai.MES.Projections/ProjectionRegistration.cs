using Marten;
using Marten.Events.Projections;

namespace LKvitai.MES.Projections;

/// <summary>
/// Projection registration extension for Marten configuration.
/// </summary>
public static class ProjectionRegistration
{
    public static void RegisterProjections(this StoreOptions options)
    {
        options.Projections.Add<ActiveHardLocksProjection>(ProjectionLifecycle.Inline);

        options.Projections.Add<LocationBalanceProjection>(ProjectionLifecycle.Async);
        options.Projections.Add<AvailableStockProjection>(ProjectionLifecycle.Async);
        options.Projections.Add<HandlingUnitProjection>(ProjectionLifecycle.Async);
        options.Projections.Add<ReservationSummaryProjection>(ProjectionLifecycle.Async);

        // Master-data operational projections.
        options.Projections.Add<ActiveReservationsProjection>(ProjectionLifecycle.Async);
        options.Projections.Add<InboundShipmentSummaryProjection>(ProjectionLifecycle.Async);
        options.Projections.Add<AdjustmentHistoryProjection>(ProjectionLifecycle.Async);
    }
}
