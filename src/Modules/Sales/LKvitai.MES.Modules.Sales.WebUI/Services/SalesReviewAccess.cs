using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace LKvitai.MES.Modules.Sales.WebUI.Services;

/// <summary>
/// Test-only design review entry. A secret URL is exchanged once for a short-lived,
/// Sales-path cookie; the URL is immediately redirected without its key. Review
/// users receive only the in-memory sample orders, never legacy SQL results.
/// Removing the two SalesReview settings disables entry and existing cookies.
/// </summary>
public static class SalesReviewAccess
{
    public const string SelectorScheme = "SalesReviewSelector";
    public const string CookieScheme = "SalesReviewCookie";
    public const string CookieName = "lkvitai.sales.review";
    public const string ReviewClaim = "sales_review";
    private const string KeyIdClaim = "sales_review_key_id";

    public static IServiceCollection AddSalesReviewAccess(
        this IServiceCollection services,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = SelectorScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddPolicyScheme(SelectorScheme, null, options =>
            {
                options.ForwardDefaultSelector = context =>
                    context.Request.Cookies.ContainsKey(CookieName)
                    && TryGetSettings(environment, configuration, out _, out _)
                        ? CookieScheme
                        : CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(CookieScheme, options =>
            {
                options.Cookie.Name = CookieName;
                options.Cookie.Path = "/sales";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.SlidingExpiration = false;
                options.ExpireTimeSpan = TimeSpan.FromHours(12);
                options.Events.OnValidatePrincipal = context =>
                {
                    if (!TryGetSettings(environment, configuration, out var key, out _)
                        || context.Principal?.HasClaim(ReviewClaim, "true") != true
                        || context.Principal.FindFirstValue(KeyIdClaim) != KeyId(key))
                    {
                        context.RejectPrincipal();
                    }

                    return Task.CompletedTask;
                };
            });

        return services;
    }

    public static IApplicationBuilder UseSalesReviewAccess(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            // UsePathBase has already removed /sales from Request.Path here.
            if (HttpMethods.IsGet(context.Request.Method)
                && context.Request.Path == "/"
                && context.Request.Query.TryGetValue("review", out var suppliedKey))
            {
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["Referrer-Policy"] = "no-referrer";

                var environment = context.RequestServices.GetRequiredService<IHostEnvironment>();
                var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
                if (suppliedKey.Count != 1
                    || !TryGetSettings(environment, configuration, out var key, out var expiresAt)
                    || !ValidKey(suppliedKey[0], key))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "sales-review"),
                    new Claim(ClaimTypes.Name, "Design review"),
                    new Claim(ReviewClaim, "true"),
                    new Claim(KeyIdClaim, KeyId(key))
                };
                var sessionExpiresAt = DateTimeOffset.UtcNow.AddHours(12);
                if (expiresAt < sessionExpiresAt) sessionExpiresAt = expiresAt;

                await context.SignInAsync(CookieScheme,
                    new ClaimsPrincipal(new ClaimsIdentity(claims, CookieScheme)),
                    new AuthenticationProperties { IsPersistent = false, ExpiresUtc = sessionExpiresAt });
                context.Response.Redirect($"{context.Request.PathBase}/");
                return;
            }

            // The shared topbar posts here. Sign out the review cookie too.
            if (HttpMethods.IsPost(context.Request.Method)
                && context.Request.Path == "/auth/logout"
                && context.User.HasClaim(ReviewClaim, "true"))
            {
                await context.SignOutAsync(CookieScheme);
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                context.Response.Redirect("/portal/login.html?returnUrl=%2Fsales%2F");
                return;
            }

            await next(context);
        });
    }

    private static bool TryGetSettings(
        IHostEnvironment environment,
        IConfiguration configuration,
        out string key,
        out DateTimeOffset expiresAt)
    {
        key = configuration["SalesReview:Key"] ?? "";
        expiresAt = default;
        return string.Equals(environment.EnvironmentName, "Test", StringComparison.OrdinalIgnoreCase)
            && key.Length == 64
            && key.All(Uri.IsHexDigit)
            && DateTimeOffset.TryParse(configuration["SalesReview:ExpiresAtUtc"],
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

    private static string KeyId(string key) => Convert.ToHexString(
        SHA256.HashData(Convert.FromHexString(key)));
}
