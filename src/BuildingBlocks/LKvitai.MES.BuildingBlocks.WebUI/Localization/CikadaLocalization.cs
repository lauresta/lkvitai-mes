using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LKvitai.MES.BuildingBlocks.WebUI.Localization;

/// <summary>A UI language offered by the shared header switcher.</summary>
public sealed record CikadaLanguage(string Code, string Label, string NativeName);

/// <summary>
/// Shared EN/LT UI localization for every Cikada.MES WebUI host.
///
/// The selected language lives in the standard ASP.NET Core culture cookie
/// (<see cref="CookieRequestCultureProvider.DefaultCookieName"/>) written with
/// <c>Path=/</c> and the same domain as the Portal auth cookie
/// (<c>PortalAuth:CookieDomain</c>), so a choice made in any module applies to
/// Portal, Warehouse, Sales, Frontline, Shopfloor and Scanning alike.
///
/// Only the UI culture (resource lookup) follows the cookie. The formatting
/// culture (numbers, dates, decimal separator) intentionally stays at the
/// host default so switching language never changes how numeric inputs are
/// parsed or how amounts are rendered.
///
/// Host wiring (see docs/i18n/README.md):
/// <code>
/// builder.Services.AddCikadaLocalization(builder.Configuration);
/// ...
/// app.UsePathBase(...);
/// app.UseCikadaRequestLocalization();   // before UseRouting / MapBlazorHub
/// ...
/// app.MapCikadaCultureEndpoint();       // GET culture/set?culture=lt&amp;returnUrl=/...
/// </code>
/// </summary>
public static class CikadaLocalization
{
    public const string DefaultUiCulture = "en";

    /// <summary>Relative (path-base aware) endpoint used by the header switcher.</summary>
    public const string SetCultureEndpoint = "culture/set";

    public const string CookieDomainConfigKey = "PortalAuth:CookieDomain";

    public static IReadOnlyList<CikadaLanguage> Languages { get; } =
    [
        new("en", "EN", "English"),
        new("lt", "LT", "Lietuvių")
    ];

    public static string CookieName => CookieRequestCultureProvider.DefaultCookieName;

    public static IServiceCollection AddCikadaLocalization(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddLocalization();
        services.Configure<RequestLocalizationOptions>(options =>
        {
            var formattingCulture = CultureInfo.CurrentCulture;
            var uiCultures = Languages.Select(x => new CultureInfo(x.Code)).ToList();

            options.DefaultRequestCulture = new RequestCulture(formattingCulture, new CultureInfo(DefaultUiCulture));
            options.SupportedCultures = [formattingCulture];
            options.SupportedUICultures = uiCultures;
            options.FallBackToParentUICultures = true;
            options.RequestCultureProviders =
            [
                new CookieRequestCultureProvider { CookieName = CookieName }
            ];
        });

        return services;
    }

    public static IApplicationBuilder UseCikadaRequestLocalization(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseRequestLocalization();
    }

    public static IEndpointConventionBuilder MapCikadaCultureEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints.MapGet("/" + SetCultureEndpoint, (HttpContext context, string? culture, string? returnUrl) =>
            {
                var selected = Languages.FirstOrDefault(x =>
                    string.Equals(x.Code, culture, StringComparison.OrdinalIgnoreCase))?.Code ?? DefaultUiCulture;

                var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
                var domain = configuration[CookieDomainConfigKey];

                context.Response.Cookies.Append(
                    CookieName,
                    CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(selected)),
                    new CookieOptions
                    {
                        Path = "/",
                        Domain = string.IsNullOrWhiteSpace(domain) ? null : domain,
                        Expires = DateTimeOffset.UtcNow.AddYears(1),
                        IsEssential = true,
                        HttpOnly = true,
                        SameSite = SameSiteMode.Lax,
                        // Always Secure: test/prod are HTTPS behind the proxy, and browsers
                        // treat http://localhost as a secure context for local dev.
                        Secure = true
                    });

                return Results.LocalRedirect(NormalizeReturnUrl(context, returnUrl));
            })
            .AllowAnonymous();
    }

    private static string NormalizeReturnUrl(HttpContext context, string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) &&
            returnUrl.StartsWith('/') &&
            !returnUrl.StartsWith("//", StringComparison.Ordinal) &&
            !returnUrl.StartsWith("/\\", StringComparison.Ordinal))
        {
            return returnUrl;
        }

        return context.Request.PathBase.HasValue ? context.Request.PathBase + "/" : "/";
    }
}
