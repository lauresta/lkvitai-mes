using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace LKvitai.MES.Modules.Sales.WebUI.Services;

/// <summary>
/// Preserve valid design-review isolation without allowing an invalid review
/// cookie to shadow the native Portal session. A single authenticated principal
/// is selected; review and Portal identities are never combined.
/// </summary>
public sealed class SalesReviewSelectorHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IWebHostEnvironment environment,
    IConfiguration configuration) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Cookies.ContainsKey(SalesReviewAccess.CookieName)
            && SalesReviewAccess.IsConfigured(environment, configuration))
        {
            var review = await Context.AuthenticateAsync(SalesReviewAccess.CookieScheme);
            if (review.Succeeded) return review;
        }

        // This is validation by the existing Portal cookie handler, not trust
        // based on cookie presence. Expired/tampered Portal cookies still fail.
        return await Context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
