using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using LKvitai.MES.Modules.Warehouse.Qa;
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
            options.ExpireTimeSpan = TimeSpan.FromHours(4);
            options.Events.OnValidatePrincipal = context =>
            {
                var settings = ReadSettings(environment, configuration);
                if (settings is null
                    || context.Principal?.FindFirstValue(KeyIdClaim) != KeyId(settings.Key))
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
            var settings = ReadSettings(environment, configuration);
            if (settings is null)
            {
                await next();
                return;
            }

            var hasHeaderKey = context.Request.Headers.TryGetValue("X-Warehouse-QA-Key", out var headerKey);
            if (HttpMethods.IsGet(context.Request.Method)
                && context.Request.Path == "/"
                && hasHeaderKey)
            {
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["Referrer-Policy"] = "no-referrer";
                var supplied = headerKey;
                if (supplied.Count != 1
                    || !ValidKey(supplied[0], settings.Key))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                var sessionExpires = DateTimeOffset.UtcNow.AddHours(4);
                if (settings.ExpiresAt < sessionExpires) sessionExpires = settings.ExpiresAt;
                var proof = context.RequestServices.GetRequiredService<WarehouseQaProof>()
                    .Create(sessionExpires);
                context.Response.Headers["X-Warehouse-QA-Proof"] = proof;
                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, WarehouseQaProof.UserId),
                    new(ClaimTypes.Name, "Warehouse QA"),
                    new("warehouse_access_token", proof),
                    new(KeyIdClaim, KeyId(settings.Key))
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

    private sealed record QaSettings(string Key, DateTimeOffset ExpiresAt);

    private static QaSettings? ReadSettings(IHostEnvironment environment, IConfiguration configuration)
    {
        if (!environment.IsEnvironment("Test")) return null;
        var configuredKey = configuration["WarehouseQa:Key"];
        if (configuredKey is null || configuredKey.Length != 64 || !configuredKey.All(Uri.IsHexDigit))
            return null;
        if (!DateTimeOffset.TryParse(configuration["WarehouseQa:ExpiresAtUtc"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var expiry) || expiry <= DateTimeOffset.UtcNow || expiry > DateTimeOffset.UtcNow.AddHours(4))
            return null;
        return new QaSettings(configuredKey, expiry);
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
