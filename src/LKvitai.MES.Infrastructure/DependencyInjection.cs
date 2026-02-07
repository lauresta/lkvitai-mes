using LKvitai.MES.Application.Orchestration;
using LKvitai.MES.Application.Ports;
using LKvitai.MES.Application.Projections;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.Infrastructure.Projections;
using Microsoft.Extensions.DependencyInjection;

namespace LKvitai.MES.Infrastructure;

/// <summary>
/// Infrastructure layer dependency injection configuration
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Register projection rebuild service (MITIGATION V-5)
        services.AddScoped<IProjectionRebuildService, ProjectionRebuildService>();

        // Repository implementations (Application ports)
        services.AddScoped<IReservationRepository, MartenReservationRepository>();
        services.AddScoped<IActiveHardLocksRepository, MartenActiveHardLocksRepository>();
        services.AddScoped<ILocationBalanceRepository, MartenLocationBalanceRepository>();
        services.AddScoped<IStockLedgerRepository, MartenStockLedgerRepository>();

        // Orchestration implementations
        services.AddScoped<IStartPickingOrchestration, MartenStartPickingOrchestration>();

        return services;
    }
}
