using LKvitai.MES.Application.Projections;
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
        
        // Additional infrastructure services to be registered here
        
        return services;
    }
}
