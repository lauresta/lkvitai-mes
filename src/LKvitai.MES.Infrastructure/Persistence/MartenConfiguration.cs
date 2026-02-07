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
            
            // Event store configuration per blueprint
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
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "warehouse");
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
