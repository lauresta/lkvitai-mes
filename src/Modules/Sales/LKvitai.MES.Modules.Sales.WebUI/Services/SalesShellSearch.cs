using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.BuildingBlocks.Search;
using LKvitai.MES.BuildingBlocks.WebUI.Localization;
using LKvitai.MES.Modules.Sales.WebUI.Resources;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Localization;

namespace LKvitai.MES.Modules.Sales.WebUI.Services;

/// <summary>
/// Sales leg of the portal "Search everything" box (issue #375): <c>GET {PathBase}/shell/search</c>.
/// The shared top bar calls it from the browser with the Portal cookie, from whichever module the
/// user is in. The Sales API accepts only the purpose-bound Sales credential, which only this host
/// can mint from the cookie, so this endpoint relays <c>GET /api/sales/search</c> exactly the way
/// <see cref="SalesApiClient"/> calls the orders list, and adds this module's screens.
/// </summary>
public static class SalesShellSearch
{
    public const string HttpClientName = "SalesApi";
    public const string ApiPath = "/api/sales/search";

    public static IEndpointConventionBuilder MapSalesShellSearch(this IEndpointRouteBuilder app)
        => app.MapGet("/shell/search", async (
            HttpContext context, string? q, int? limit, IHttpClientFactory clients,
            SalesCredentialProtector protector, IStringLocalizer<SalesResources> text, ILoggerFactory loggers) =>
        {
            var auth = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = auth.Succeeded ? auth.Principal : null;
            if (ShellSearchRelay.Reject(context, principal is not null, q, limit, out var input) is { } rejected) return rejected;

            var screens = ScreenSearch.Match(Screens(text), input!.Query, input.Limit, input.PathBase);

            // The review selector serves sample orders only; it never gets a Sales credential.
            if (context.User.HasClaim(SalesCredentialProtector.ReviewClaim, "true")) return Results.Ok(screens);

            var proof = SalesCredentialProtector.PortalIdentity(principal!)?.FindFirst(SalesCredentialProtector.SessionClaim)?.Value;
            var credential = protector.CreateApi(proof);
            if (credential is null) return Results.StatusCode(StatusCodes.Status403Forbidden);

            return await ShellSearchRelay.RelayAsync(context, input, screens, clients.CreateClient(HttpClientName),
                ApiPath, credential, loggers.CreateLogger(typeof(SalesShellSearch).FullName!));
        });

    private static IEnumerable<string> Cultures => CikadaLocalization.Languages.Select(x => x.Code);

    /// <summary>Sales has no sidebar: its one screen is the orders list at the module root.</summary>
    public static IReadOnlyList<ScreenEntry> Screens(IStringLocalizer<SalesResources> text)
        =>
        [
            new(string.Empty, text["Common.Orders"].Value, text["Orders.PageTitle"].Value,
                [.. ScreenLabels.InCultures(text, "Common.Orders", Cultures), .. ScreenLabels.InCultures(text, "Orders.PageTitle", Cultures)]),
        ];
}
