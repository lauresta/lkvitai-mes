using LKvitai.MES.Application.ConsistencyChecks;
using LKvitai.MES.Application.EventVersioning;
using LKvitai.MES.Application.Orchestration;
using LKvitai.MES.Application.Ports;
using LKvitai.MES.Application.Projections;
using LKvitai.MES.Application.Services;
using LKvitai.MES.Infrastructure.BackgroundJobs;
using LKvitai.MES.Infrastructure.EventVersioning;
using LKvitai.MES.Infrastructure.Imports;
using LKvitai.MES.Infrastructure.Locking;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.Infrastructure.Projections;
using Microsoft.Extensions.DependencyInjection;

namespace LKvitai.MES.Infrastructure;

/// <summary>
/// Infrastructure layer dependency injection configuration.
///
/// NOTE: MediatR pipeline behaviors (IdempotencyBehavior, ValidationBehavior, etc.)
/// are registered in Api/Configuration/MediatRConfiguration.cs — NOT here.
/// Registering them in both places causes the behavior to run twice.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<IDistributedLock, PostgresDistributedLock>();

        // Register projection rebuild service (MITIGATION V-5)
        services.AddScoped<IProjectionRebuildService, ProjectionRebuildService>();
        services.AddScoped<IProjectionCleanupService, ProjectionCleanupService>();
        services.AddScoped<IProjectionHealthService, ProjectionHealthService>();
        services.AddScoped<ISkuGenerationService, SkuGenerationService>();
        services.AddScoped<IExcelTemplateService, ExcelTemplateService>();
        services.AddScoped<IMasterDataImportService, MasterDataImportService>();

        // Repository implementations (Application ports)
        services.AddScoped<IReservationRepository, MartenReservationRepository>();
        services.AddScoped<IReservationReadModelQueryService, MartenReservationReadModelQueryService>();
        services.AddScoped<IActiveHardLocksRepository, MartenActiveHardLocksRepository>();
        services.AddScoped<ILocationBalanceRepository, MartenLocationBalanceRepository>();
        services.AddScoped<IAvailableStockRepository, MartenAvailableStockRepository>();
        services.AddScoped<IStockLedgerRepository, MartenStockLedgerRepository>();
        services.AddScoped<IProjectionVerificationDataAccess, MartenProjectionVerificationDataAccess>();

        // Orchestration implementations
        services.AddScoped<IStartPickingOrchestration, MartenStartPickingOrchestration>();
        services.AddScoped<IReceiveGoodsOrchestration, MartenReceiveGoodsOrchestration>();
        services.AddScoped<IAllocateReservationOrchestration, MartenAllocateReservationOrchestration>();
        services.AddScoped<IPickStockOrchestration, MartenPickStockOrchestration>();

        // [HOTFIX CRIT-01] Balance guard lock for serializing balance-affecting operations
        services.AddSingleton<IBalanceGuardLockFactory, PostgresBalanceGuardLockFactory>();

        // Command idempotency store (Marten document — application port impl)
        services.AddScoped<IProcessedCommandStore, MartenProcessedCommandStore>();
        services.AddSingleton<IIdempotencyCleanupService, IdempotencyCleanupService>();
        services.AddSingleton<IEventUpcaster, StockMovedV1ToStockMovedEventUpcaster>();
        services.AddSingleton<IEventSchemaVersionRegistry, EventSchemaVersionRegistry>();

        // Consistency checks
        services.AddScoped<IConsistencyCheck, StuckReservationCheck>();
        services.AddScoped<IConsistencyCheck, OrphanHardLockCheck>();

        return services;
    }
}
