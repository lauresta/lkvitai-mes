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
    }
}
