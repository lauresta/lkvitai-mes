using Marten;
using Marten.Events.Projections;
using Marten.Events.Daemon.Resiliency;
using System.Reflection;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Domain.Aggregates;
using LKvitai.MES.Infrastructure.EventVersioning;

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
        services.AddMarten((StoreOptions options) =>
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

            RegisterMartenEventTypes(options);
            RegisterMartenDocumentAliases(options);

            options.Projections.Snapshot<Valuation>(SnapshotLifecycle.Inline);
            options.Projections.Snapshot<ItemValuation>(SnapshotLifecycle.Inline);
            
            // Performance tuning
            options.Events.MetadataConfig.HeadersEnabled = true;
            options.Events.MetadataConfig.CausationIdEnabled = true;
            options.Events.MetadataConfig.CorrelationIdEnabled = true;
            
            // Allow composition root (API layer) to register projections
            // This avoids Infrastructure referencing Projections directly
            configureProjections?.Invoke(options);

            // Event schema evolution example: StockMoved v1 -> v2.
            options.Events.Upcast<StockMovedV1Event, StockMovedEvent>(
                oldEvent => new StockMovedV1ToStockMovedEventUpcaster().Upcast(oldEvent));
        })
        .AddAsyncDaemon(DaemonMode.Solo);
        
        return services;
    }

    private static void RegisterMartenEventTypes(StoreOptions options)
    {
        RegisterEventType<AgnumExportStartedEvent>(options);
        RegisterEventType<AgnumExportCompletedEvent>(options);
        RegisterEventType<AgnumExportFailedEvent>(options);
        RegisterEventType<CycleCountScheduledEvent>(options);
        RegisterEventType<CountRecordedEvent>(options);
        RegisterEventType<CycleCountCompletedEvent>(options);
        RegisterEventType<HandlingUnitCreatedEvent>(options);
        RegisterEventType<LineAddedToHandlingUnitEvent>(options);
        RegisterEventType<LineRemovedFromHandlingUnitEvent>(options);
        RegisterEventType<HandlingUnitSealedEvent>(options);
        RegisterEventType<HandlingUnitSplitEvent>(options);
        RegisterEventType<HandlingUnitMergedEvent>(options);
        RegisterEventType<InboundShipmentCreatedEvent>(options);
        RegisterEventType<GoodsReceivedEvent>(options);
        RegisterEventType<PickCompletedEvent>(options);
        RegisterEventType<StockAdjustedEvent>(options);
        RegisterEventType<ReservationCreatedMasterDataEvent>(options);
        RegisterEventType<ReservationReleasedMasterDataEvent>(options);
        RegisterEventType<QCPassedEvent>(options);
        RegisterEventType<QCFailedEvent>(options);
        RegisterEventType<OutboundOrderCreatedEvent>(options);
        RegisterEventType<ShipmentPackedEvent>(options);
        RegisterEventType<ShipmentDispatchedEvent>(options);
        RegisterEventType<ReservationCreatedEvent>(options);
        RegisterEventType<StockAllocatedEvent>(options);
        RegisterEventType<PickingStartedEvent>(options);
        RegisterEventType<ReservationConsumedEvent>(options);
        RegisterEventType<ReservationCancelledEvent>(options);
        RegisterEventType<ReservationBumpedEvent>(options);
        RegisterEventType<SalesOrderCreatedEvent>(options);
        RegisterEventType<SalesOrderAllocatedEvent>(options);
        RegisterEventType<SalesOrderReleasedEvent>(options);
        RegisterEventType<SalesOrderCancelledEvent>(options);
        RegisterEventType<StockMovedV1Event>(options);
        RegisterEventType<StockMovedEvent>(options);
        RegisterEventType<TransferCreatedEvent>(options);
        RegisterEventType<TransferApprovedEvent>(options);
        RegisterEventType<TransferExecutedEvent>(options);
        RegisterEventType<TransferCompletedEvent>(options);
        RegisterEventType<ValuationInitialized>(options);
        RegisterEventType<CostAdjusted>(options);
        RegisterEventType<LandedCostAllocated>(options);
        RegisterEventType<StockWrittenDown>(options);
        RegisterEventType<LandedCostApplied>(options);
        RegisterEventType<WrittenDown>(options);
    }

    private static void RegisterMartenDocumentAliases(StoreOptions options)
    {
        RegisterDocumentAlias<Valuation>(options, "LKvitai.MES.Domain.Aggregates", "LKvitai.MES.Domain");
        RegisterDocumentAlias<ItemValuation>(options, "LKvitai.MES.Domain.Aggregates", "LKvitai.MES.Domain");
        RegisterDocumentAlias<ActiveHardLockView>(options, "LKvitai.MES.Contracts.ReadModels", "LKvitai.MES.Contracts");
        RegisterDocumentAlias<LocationBalanceView>(options, "LKvitai.MES.Contracts.ReadModels", "LKvitai.MES.Contracts");
        RegisterDocumentAlias<AvailableStockView>(options, "LKvitai.MES.Contracts.ReadModels", "LKvitai.MES.Contracts");
        RegisterDocumentAlias<HandlingUnitView>(options, "LKvitai.MES.Contracts.ReadModels", "LKvitai.MES.Contracts");
        RegisterDocumentAlias<ReservationSummaryView>(options, "LKvitai.MES.Contracts.ReadModels", "LKvitai.MES.Contracts");
        RegisterDocumentAlias<ActiveReservationView>(options, "LKvitai.MES.Contracts.ReadModels", "LKvitai.MES.Contracts");
        RegisterDocumentAlias<InboundShipmentSummaryView>(options, "LKvitai.MES.Contracts.ReadModels", "LKvitai.MES.Contracts");
        RegisterDocumentAlias<AdjustmentHistoryView>(options, "LKvitai.MES.Contracts.ReadModels", "LKvitai.MES.Contracts");

        RegisterDocumentAliasByTypeName(options, "LKvitai.MES.Sagas.PickStockSagaState, LKvitai.MES.Sagas");
        RegisterDocumentAliasByTypeName(options, "LKvitai.MES.Sagas.ReceiveGoodsSagaState, LKvitai.MES.Sagas");
        RegisterDocumentAliasByTypeName(options, "LKvitai.MES.Sagas.AgnumExportSagaState, LKvitai.MES.Sagas");
    }

    private static void RegisterEventType<TEvent>(StoreOptions options)
        where TEvent : class
    {
        var oldQualifiedName = $"LKvitai.MES.Contracts.Events.{typeof(TEvent).Name}, LKvitai.MES.Contracts";
        options.Events.MapEventType<TEvent>(oldQualifiedName);
        options.Events.AddEventType<TEvent>();
    }

    private static void RegisterDocumentAlias<TDocument>(
        StoreOptions options,
        string oldNamespace,
        string oldAssembly)
        where TDocument : class
    {
        var oldQualifiedName = $"{oldNamespace}.{typeof(TDocument).Name}, {oldAssembly}";
        options.Schema.For<TDocument>().DocumentAlias(oldQualifiedName);
    }

    private static void RegisterDocumentAliasByTypeName(StoreOptions options, string oldQualifiedName)
    {
        var documentType = Type.GetType(oldQualifiedName, throwOnError: false);
        if (documentType is null)
        {
            return;
        }

        var registerMethod = typeof(MartenConfiguration)
            .GetMethod(nameof(RegisterDocumentAliasForResolvedType), BindingFlags.NonPublic | BindingFlags.Static)
            ?.MakeGenericMethod(documentType);
        registerMethod?.Invoke(null, new object[] { options, oldQualifiedName });
    }

    private static void RegisterDocumentAliasForResolvedType<TDocument>(StoreOptions options, string oldQualifiedName)
        where TDocument : class
    {
        options.Schema.For<TDocument>().DocumentAlias(oldQualifiedName);
    }
    
    /// <summary>
    /// Register EF Core DbContext for state-based aggregates
    /// </summary>
    public static IServiceCollection AddWarehouseDbContext(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var rawConnectionString = configuration.GetConnectionString("WarehouseDb")
            ?? throw new InvalidOperationException("WarehouseDb connection string not found");

        var builder = new NpgsqlConnectionStringBuilder(rawConnectionString)
        {
            MinPoolSize = 10,
            MaxPoolSize = 100,
            ConnectionLifetime = 300,
            ConnectionIdleLifetime = 60,
            Timeout = 30
        };

        services.AddDbContext<WarehouseDbContext>((sp, options) =>
        {
            var poolMonitoringInterceptor = sp.GetRequiredService<ConnectionPoolMonitoringInterceptor>();
            options.UseNpgsql(builder.ConnectionString, npgsqlOptions =>
            {
                // Keep EF migration metadata in `public` to match EF schema placement.
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "public");
                npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            });
            options.AddInterceptors(poolMonitoringInterceptor);
            
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
