using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LKvitai.MES.BuildingBlocks.PortalAuth;

public static class PortalAuthApplicationBuilderExtensions
{
    private const string TestEnvironmentName = "Test";

    /// <summary>
    /// Applies the standard portal hosting pipeline: production-only exception handler + HSTS,
    /// plus HTTPS redirection for non-Development and non-Test environments.
    /// </summary>
    public static IApplicationBuilder UsePortalSecureHosting(
        this IApplicationBuilder app,
        IWebHostEnvironment environment,
        string errorPath = "/Error")
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(environment);

        if (!environment.IsDevelopment())
        {
            app.UseExceptionHandler(errorPath);
            app.UseHsts();
        }

        if (!environment.IsDevelopment()
            && !string.Equals(environment.EnvironmentName, TestEnvironmentName, StringComparison.OrdinalIgnoreCase))
        {
            app.UseHttpsRedirection();
        }

        return app;
    }

    /// <summary>
    /// Maps the shared portal logout endpoint that signs the user out of the cookie scheme
    /// and redirects to the portal login page.
    /// </summary>
    public static IEndpointRouteBuilder MapPortalLogout(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost(PortalAuthDefaults.LogoutPath, async (HttpContext httpContext) =>
        {
            await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            httpContext.DeleteLegacyPortalAuthCookies();
            return Results.Redirect(ResolveLoginPath(httpContext));
        });

        return endpoints;
    }

    public static void DeleteLegacyPortalAuthCookies(this HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
        var configuredCookieDomain = configuration[PortalAuthDefaults.CookieDomainConfigKey];
        var paths = new[] { "/portal", "/warehouse", httpContext.Request.PathBase.Value }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => NormalizePath(path!))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var domains = ResolveLegacyCookieDomains(configuredCookieDomain);

        foreach (var path in paths)
        {
            foreach (var domain in domains)
            {
                httpContext.Response.Cookies.Delete(PortalAuthDefaults.CookieName, new CookieOptions
                {
                    Domain = domain,
                    Path = path,
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax
                });
            }
        }
    }

    private static string ResolveLoginPath(HttpContext httpContext)
    {
        var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
        var configuredBasePath = configuration[PortalAuthDefaults.LoginBasePathConfigKey];
        if (!string.IsNullOrWhiteSpace(configuredBasePath))
        {
            return $"{NormalizePath(configuredBasePath)}{PortalAuthDefaults.LoginPath}";
        }

        return $"{httpContext.Request.PathBase}{PortalAuthDefaults.LoginPath}";
    }

    private static string NormalizePath(string path)
    {
        var normalized = path.Trim().TrimEnd('/');
        return normalized.StartsWith("/", StringComparison.Ordinal) ? normalized : $"/{normalized}";
    }

    private static IEnumerable<string?> ResolveLegacyCookieDomains(string? configuredCookieDomain)
    {
        yield return null;

        if (!string.IsNullOrWhiteSpace(configuredCookieDomain))
        {
            yield return configuredCookieDomain;
        }

        yield return ".mes-test.lauresta.com";
        yield return ".lauresta.com";
    }
}
