using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;

/// <summary>Exchanges a temporary test-only key for a Warehouse-only QA cookie.</summary>
public static class WarehouseQaAccess
{
    private const string Scheme = "WarehouseQaCookie";
    private const string CookieName = "lkvitai.warehouse.qa";
    private const string KeyIdClaim = "warehouse_qa_key_id";

    public static IServiceCollection AddWarehouseQaAccess(
        this IServiceCollection services, IHostEnvironment environment, IConfiguration configuration)
    {
        services.AddAuthentication().AddCookie(Scheme, options =>
        {
            options.Cookie.Name = CookieName;
            options.Cookie.Path = "/warehouse";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.SlidingExpiration = false;
            options.ExpireTimeSpan = TimeSpan.FromHours(12);
            options.Events.OnValidatePrincipal = context =>
            {
                if (!TryGetSettings(environment, configuration, out var key, out _)
                    || context.Principal?.FindFirstValue(KeyIdClaim) != KeyId(key))
                    context.RejectPrincipal();
                return Task.CompletedTask;
            };
        });
        return services;
    }

    public static IApplicationBuilder UseWarehouseQaAccess(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var environment = context.RequestServices.GetRequiredService<IHostEnvironment>();
            var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
            if (!TryGetSettings(environment, configuration, out var key, out var expiresAt))
            {
                await next();
                return;
            }

            if (HttpMethods.IsGet(context.Request.Method)
                && context.Request.Path == "/"
                && context.Request.Query.TryGetValue("qa", out var supplied))
            {
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["Referrer-Policy"] = "no-referrer";
                if (supplied.Count != 1 || !ValidKey(supplied[0], key))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                var sessionExpires = DateTimeOffset.UtcNow.AddHours(12);
                if (expiresAt < sessionExpires) sessionExpires = expiresAt;
                var proof = context.RequestServices.GetRequiredService<WarehouseQaProof>()
                    .Create(sessionExpires);
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, WarehouseQaProof.UserId),
                    new(ClaimTypes.Name, "Warehouse QA"),
                    new("warehouse_access_token", proof),
                    new(KeyIdClaim, KeyId(key))
                };
                claims.AddRange(WarehouseQaProof.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
                await context.SignInAsync(Scheme,
                    new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme)),
                    new AuthenticationProperties { IsPersistent = false, ExpiresUtc = sessionExpires });
                context.Response.Redirect($"{context.Request.PathBase}/");
                return;
            }

            if (context.Request.Cookies.ContainsKey(CookieName))
            {
                var result = await context.AuthenticateAsync(Scheme);
                if (result.Succeeded && result.Principal is not null)
                    context.User = result.Principal;
            }

            await next();
        });

    private static bool TryGetSettings(
        IHostEnvironment environment, IConfiguration configuration,
        out string key, out DateTimeOffset expiresAt)
    {
        key = configuration["WarehouseQa:Key"] ?? "";
        expiresAt = default;
        return string.Equals(environment.EnvironmentName, "Test", StringComparison.OrdinalIgnoreCase)
            && key.Length == 64
            && key.All(Uri.IsHexDigit)
            && DateTimeOffset.TryParse(configuration["WarehouseQa:ExpiresAtUtc"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out expiresAt)
            && expiresAt > DateTimeOffset.UtcNow;
    }

    private static bool ValidKey(string? supplied, string configured)
    {
        if (supplied?.Length != 64) return false;
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(supplied), Convert.FromHexString(configured));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string KeyId(string key) =>
        Convert.ToHexString(SHA256.HashData(Convert.FromHexString(key)));
}
