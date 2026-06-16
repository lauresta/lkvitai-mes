using FluentValidation;
using LKvitai.MES.Modules.Shopfloor.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LKvitai.MES.Modules.Shopfloor.Application;

public static class ShopfloorApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Registers Shopfloor application services + FluentValidation validators.
    /// Repositories, the legacy source and the unit of work are wired by the
    /// Infrastructure layer.
    /// </summary>
    public static IServiceCollection AddShopfloorApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddValidatorsFromAssemblyContaining<ShopfloorApplicationMarker>(ServiceLifetime.Singleton);

        services.AddScoped<IWorkCenterService, WorkCenterService>();
        services.AddScoped<IWorkStationService, WorkStationService>();
        services.AddScoped<IWorkflowService, WorkflowService>();
        services.AddScoped<ILegacyProductTypeService, LegacyProductTypeService>();
        services.AddScoped<IMappingService, MappingService>();

        return services;
    }
}

/// <summary>Assembly marker for validator scanning.</summary>
public sealed class ShopfloorApplicationMarker
{
}
