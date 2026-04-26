using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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
            return Results.Redirect(PortalAuthDefaults.LoginPath);
        });

        return endpoints;
    }
}
