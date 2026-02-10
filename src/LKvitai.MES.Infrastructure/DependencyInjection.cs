using LKvitai.MES.Application.ConsistencyChecks;
using LKvitai.MES.Application.Orchestration;
using LKvitai.MES.Application.Ports;
using LKvitai.MES.Application.Projections;
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

        // Repository implementations (Application ports)
        services.AddScoped<IReservationRepository, MartenReservationRepository>();
        services.AddScoped<IActiveHardLocksRepository, MartenActiveHardLocksRepository>();
        services.AddScoped<ILocationBalanceRepository, MartenLocationBalanceRepository>();
        services.AddScoped<IAvailableStockRepository, MartenAvailableStockRepository>();
        services.AddScoped<IStockLedgerRepository, MartenStockLedgerRepository>();

        // Orchestration implementations
        services.AddScoped<IStartPickingOrchestration, MartenStartPickingOrchestration>();
        services.AddScoped<IReceiveGoodsOrchestration, MartenReceiveGoodsOrchestration>();
        services.AddScoped<IAllocateReservationOrchestration, MartenAllocateReservationOrchestration>();
        services.AddScoped<IPickStockOrchestration, MartenPickStockOrchestration>();

        // [HOTFIX CRIT-01] Balance guard lock for serializing balance-affecting operations
        services.AddSingleton<IBalanceGuardLockFactory, PostgresBalanceGuardLockFactory>();

        // Command idempotency store (Marten document — application port impl)
        services.AddScoped<IProcessedCommandStore, MartenProcessedCommandStore>();

        // Consistency checks
        services.AddScoped<IConsistencyCheck, StuckReservationCheck>();
        services.AddScoped<IConsistencyCheck, OrphanHardLockCheck>();

        return services;
    }
}
