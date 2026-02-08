using Marten;
using Marten.Events.Projections;

namespace LKvitai.MES.Projections;

/// <summary>
/// Projection registration extension for Marten configuration
/// Exposes projection registration without requiring Infrastructure to reference Projections
/// </summary>
public static class ProjectionRegistration
{
    /// <summary>
    /// Registers all projections with Marten StoreOptions
    /// </summary>
    /// <param name="options">Marten StoreOptions</param>
    public static void RegisterProjections(this StoreOptions options)
    {
        // Register inline projections (MITIGATION R-4)
        // Inline projections updated atomically with events
        options.Projections.Add<ActiveHardLocksProjection>(ProjectionLifecycle.Inline);
        
        // Register async projections
        // Async projections updated by async daemon
        options.Projections.Add<LocationBalanceProjection>(ProjectionLifecycle.Async);

        // AvailableStock: async projection combining on-hand (StockMoved) and
        // hard-locked quantities (PickingStarted / ReservationConsumed / ReservationCancelled).
        // Async is sufficient because the AllocationSaga that consumes this data is itself async.
        options.Projections.Add<AvailableStockProjection>(ProjectionLifecycle.Async);

        // HandlingUnit: async projection from HU lifecycle events + StockMoved events.
        // Maintains HU state (status, location, lines) per design spec.
        // Async is consistent with all other non-inline projections.
        options.Projections.Add<HandlingUnitProjection>(ProjectionLifecycle.Async);
    }
}
