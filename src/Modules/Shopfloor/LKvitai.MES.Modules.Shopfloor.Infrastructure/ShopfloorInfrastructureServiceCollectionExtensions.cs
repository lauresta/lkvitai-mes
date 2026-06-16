using LKvitai.MES.Modules.Shopfloor.Application.Configuration;
using LKvitai.MES.Modules.Shopfloor.Application.Ports;
using LKvitai.MES.Modules.Shopfloor.Infrastructure.Legacy;
using LKvitai.MES.Modules.Shopfloor.Infrastructure.Persistence;
using LKvitai.MES.Modules.Shopfloor.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LKvitai.MES.Modules.Shopfloor.Infrastructure;

public static class ShopfloorInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddShopfloorInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var shopfloorDb = configuration.GetConnectionString("ShopfloorDb");
        if (string.IsNullOrWhiteSpace(shopfloorDb))
        {
            throw new InvalidOperationException("ConnectionStrings:ShopfloorDb is required for the Shopfloor module.");
        }

        var dbBuilder = new NpgsqlConnectionStringBuilder(shopfloorDb);
        services.AddDbContext<ShopfloorDbContext>(options =>
        {
            options.UseNpgsql(dbBuilder.ConnectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ShopfloorDbContext.Schema);
                npgsql.EnableRetryOnFailure(maxRetryCount: 3);
            });
        });

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IWorkCenterRepository, WorkCenterRepository>();
        services.AddScoped<IWorkStationRepository, WorkStationRepository>();
        services.AddScoped<IWorkflowTemplateRepository, WorkflowTemplateRepository>();
        services.AddScoped<ILegacyProductTypeRepository, LegacyProductTypeRepository>();
        services.AddScoped<IProductTypeMappingRepository, ProductTypeMappingRepository>();

        AddLegacySource(services, configuration);

        return services;
    }

    private static void AddLegacySource(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(LegacyProductTypesOptions.SectionName);
        var options = new LegacyProductTypesOptions
        {
            DataSource = string.IsNullOrWhiteSpace(section["DataSource"]) ? "Sql" : section["DataSource"]!,
            CommandTimeoutSeconds = int.TryParse(section["CommandTimeoutSeconds"], out var timeout) ? timeout : 30,
        };
        services.AddSingleton(options);

        if (string.Equals(options.DataSource, "Stub", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<ILegacyProductTypeSource, StubLegacyProductTypeSource>();
            return;
        }

        var legacyConnectionString = configuration.GetConnectionString("LKvitaiDb");
        if (string.IsNullOrWhiteSpace(legacyConnectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:LKvitaiDb is required when Shopfloor:LegacyProductTypes:DataSource = 'Sql'.");
        }

        services.AddSingleton(new SqlLegacyProductTypeSourceOptions { ConnectionString = legacyConnectionString });
        services.AddScoped<ILegacyProductTypeSource, SqlLegacyProductTypeSource>();
    }
}
