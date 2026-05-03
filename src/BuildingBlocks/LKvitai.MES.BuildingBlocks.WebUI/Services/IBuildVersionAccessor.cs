namespace LKvitai.MES.BuildingBlocks.WebUI.Services;

/// <summary>
/// Resolves the deployed <see cref="BuildVersion"/> from the runtime
/// environment. One implementation
/// (<see cref="ConfigurationBuildVersionAccessor"/>) ships in this assembly;
/// it reads the standard <c>APP_VERSION</c> / <c>GIT_SHA</c> / <c>BUILD_DATE</c>
/// keys from <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>
/// (which transparently exposes ASP.NET Core's environment-variable provider).
/// Modules add the registration via
/// <see cref="BuildVersionServiceCollectionExtensions.AddBuildVersion"/> in
/// <c>Program.cs</c> and inject <see cref="IBuildVersionAccessor"/> into
/// <c>MainLayout</c> to populate the shared topbar's "VER" slot.
/// </summary>
public interface IBuildVersionAccessor
{
    BuildVersion Get();
}
