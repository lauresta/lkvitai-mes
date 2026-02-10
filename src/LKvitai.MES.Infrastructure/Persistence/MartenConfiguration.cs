using Marten;
using Marten.Events.Daemon.Resiliency;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LKvitai.MES.Infrastructure.Persistence;

/// <summary>
/// Marten event store configuration per blueprint
/// </summary>
public static class MartenConfiguration
{
    public static IServiceCollection AddMartenEventStore(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<StoreOptions>? configureProjections = null)
    {
        services.AddMarten(options =>
        {
            var connectionString = configuration.GetConnectionString("WarehouseDb")
                ?? throw new InvalidOperationException("WarehouseDb connection string not found");
            
            options.Connection(connectionString);

            // Schema separation: EF Core relational tables stay in `public`,
            // Marten events + projection documents stay in `warehouse_events`.
            // This prevents mt_doc_* rebuild shadow tables from colliding with EF objects.
            options.DatabaseSchemaName = "warehouse_events";

            // Event store configuration per blueprint
            // ADR-001: Use string stream identity for named streams (stock-ledger-{warehouseId}, etc.)
            options.Events.StreamIdentity = Marten.Events.StreamIdentity.AsString;
            options.Events.DatabaseSchemaName = "warehouse_events";
            
            // Performance tuning
            options.Events.MetadataConfig.HeadersEnabled = true;
            options.Events.MetadataConfig.CausationIdEnabled = true;
            options.Events.MetadataConfig.CorrelationIdEnabled = true;
            
            // Allow composition root (API layer) to register projections
            // This avoids Infrastructure referencing Projections directly
            configureProjections?.Invoke(options);
        })
        .AddAsyncDaemon(DaemonMode.Solo);
        
        return services;
    }
    
    /// <summary>
    /// Register EF Core DbContext for state-based aggregates
    /// </summary>
    public static IServiceCollection AddWarehouseDbContext(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("WarehouseDb")
            ?? throw new InvalidOperationException("WarehouseDb connection string not found");
        
        services.AddDbContext<WarehouseDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                // Keep EF migration metadata in `public` to match EF schema placement.
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            });
            
            // Enable sensitive data logging in development only
            var enableSensitiveDataLogging = configuration["EnableSensitiveDataLogging"] == "true";
            if (enableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging();
            }
        });
        
        return services;
    }
}
