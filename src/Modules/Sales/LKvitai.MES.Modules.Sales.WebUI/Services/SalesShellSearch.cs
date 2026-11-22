using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

    public static IEndpointConventionBuilder MapSalesShellSearch(this IEndpointRouteBuilder app)
        => app.MapGet("/shell/search", async (
            HttpContext context, string? q, int? limit, IHttpClientFactory clients,
            SalesCredentialProtector protector, IStringLocalizer<SalesResources> text, ILoggerFactory loggers) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var fetchSite = context.Request.Headers["Sec-Fetch-Site"].ToString();
            if (fetchSite.Length != 0 && fetchSite != "same-origin") return Results.StatusCode(403);

            var auth = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!auth.Succeeded || auth.Principal is null) return Results.StatusCode(401);

            var query = SearchRequest.NormalizeQuery(q);
            if (query is null) return Results.Ok(Array.Empty<SearchResultDto>());

            var perKind = SearchRequest.ClampLimit(limit);
            var pathBase = context.Request.PathBase.Value;
            var screens = ScreenSearch.Match(Screens(text), query, perKind, pathBase);

            // The review selector serves sample orders only; it never gets a Sales credential.
            if (context.User.HasClaim(SalesCredentialProtector.ReviewClaim, "true")) return Results.Ok(screens);

            var proof = SalesCredentialProtector.PortalIdentity(auth.Principal)?.FindFirst(SalesCredentialProtector.SessionClaim)?.Value;
            var credential = protector.CreateApi(proof);
            if (credential is null) return Results.StatusCode(403);

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get,
                    $"/api/sales/search?q={Uri.EscapeDataString(query)}&limit={perKind}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential);
                using var response = await clients.CreateClient(HttpClientName).SendAsync(request, context.RequestAborted);
                // No Sales access (no legacy login mapping, denied by SQL): the user simply has no Sales group.
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return Results.StatusCode(403);
                if (response.IsSuccessStatusCode)
                {
                    var hits = await response.Content.ReadFromJsonAsync<List<SearchResultDto>>(context.RequestAborted) ?? [];
                    return Results.Ok(ShellSearchRelay.Rebase(hits, pathBase).Concat(screens));
                }

                loggers.CreateLogger(typeof(SalesShellSearch).FullName!).LogWarning(
                    "Sales search relay failed with status {StatusCode}", (int)response.StatusCode);
            }
            catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException
                || ex is OperationCanceledException && !context.RequestAborted.IsCancellationRequested)
            {
                loggers.CreateLogger(typeof(SalesShellSearch).FullName!).LogWarning(ex, "Sales search relay failed");
            }

            context.Response.Headers[SearchRequest.PartialHeader] = "1";
            return Results.Ok(screens);
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
