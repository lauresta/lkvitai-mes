using Microsoft.Extensions.DependencyInjection;

namespace LKvitai.MES.BuildingBlocks.WebUI.Services;

/// <summary>
/// DI registration helpers for the shared build-version accessor. Modules
/// call <c>builder.Services.AddBuildVersion()</c> in <c>Program.cs</c> and
/// inject <see cref="IBuildVersionAccessor"/> into <c>MainLayout.razor</c>
/// to render the env-badge "VER" cell.
/// </summary>
public static class BuildVersionServiceCollectionExtensions
{
    public static IServiceCollection AddBuildVersion(this IServiceCollection services)
    {
        services.AddSingleton<IBuildVersionAccessor, ConfigurationBuildVersionAccessor>();
        return services;
    }
}
