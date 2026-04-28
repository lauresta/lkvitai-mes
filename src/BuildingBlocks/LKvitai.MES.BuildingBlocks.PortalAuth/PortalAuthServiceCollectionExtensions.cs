using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LKvitai.MES.BuildingBlocks.PortalAuth;

public static class PortalAuthServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shared portal cookie authentication scheme and persists DataProtection
    /// keys to a path that is shared between the Portal and Warehouse WebUI hosts so the
    /// auth cookie issued by one host is decryptable by the other.
    /// </summary>
    public static IServiceCollection AddPortalCookieAuthentication(
        this IServiceCollection services,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDataProtection()
            .SetApplicationName(PortalAuthDefaults.ApplicationName)
            .PersistKeysToFileSystem(new DirectoryInfo(ResolveDataProtectionKeysPath(environment, configuration)));

        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = PortalAuthDefaults.LoginPath;
                options.AccessDeniedPath = PortalAuthDefaults.AccessDeniedPath;
                options.Cookie.Name = PortalAuthDefaults.CookieName;
                options.Cookie.Domain = ResolveCookieDomain(configuration);
                options.Cookie.Path = "/";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax;
                options.Cookie.SecurePolicy = environment.IsDevelopment()
                    ? Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest
                    : Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = PortalAuthDefaults.CookieLifetime;
            });

        return services;
    }

    private static string ResolveDataProtectionKeysPath(IHostEnvironment environment, IConfiguration configuration)
    {
        var configured = configuration[PortalAuthDefaults.DataProtectionKeysPathConfigKey];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return Path.GetFullPath(Path.Combine(
            environment.ContentRootPath,
            PortalAuthDefaults.DefaultDataProtectionKeysRelativePath));
    }

    private static string? ResolveCookieDomain(IConfiguration configuration)
    {
        var configured = configuration[PortalAuthDefaults.CookieDomainConfigKey];
        return string.IsNullOrWhiteSpace(configured) ? null : configured;
    }
}
