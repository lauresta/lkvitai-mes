using System.Net;
using System.Net.Http.Json;
using LKvitai.MES.BuildingBlocks.Search;
using LKvitai.MES.Modules.Warehouse.WebUI.Resources;
using LKvitai.MES.Modules.Warehouse.WebUI.Shared;
using Microsoft.Extensions.Localization;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

/// <summary>
/// Warehouse leg of the portal "Search everything" box (issue #375): <c>GET {PathBase}/shell/search</c>.
/// The shared top bar calls it from the browser with the Portal cookie, from whichever module the
/// user is in. The Warehouse API takes the bearer token carried in that cookie, which the browser
/// cannot read, so this endpoint relays <c>GET /api/warehouse/v1/search</c> through the same
/// "WarehouseApi" client every page uses, and adds the screens of the Warehouse sidebar.
/// </summary>
public static class WarehouseShellSearch
{
    public const string HttpClientName = "WarehouseApi";

    public static IEndpointConventionBuilder MapWarehouseShellSearch(this IEndpointRouteBuilder app)
        => app.MapGet("/shell/search", async (
            HttpContext context, string? q, int? limit, IHttpClientFactory clients,
            IStringLocalizer<WarehouseResources> text, ILoggerFactory loggers) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var fetchSite = context.Request.Headers["Sec-Fetch-Site"].ToString();
            if (fetchSite.Length != 0 && fetchSite != "same-origin") return Results.StatusCode(403);
            if (context.User.Identity?.IsAuthenticated != true) return Results.StatusCode(401);

            var query = SearchRequest.NormalizeQuery(q);
            if (query is null) return Results.Ok(Array.Empty<SearchResultDto>());

            var perKind = SearchRequest.ClampLimit(limit);
            var pathBase = context.Request.PathBase.Value;
            var screens = ScreenSearch.Match(WarehouseNavigation.Screens(text), query, perKind, pathBase);

            try
            {
                using var response = await clients.CreateClient(HttpClientName).GetAsync(
                    $"/api/warehouse/v1/search?q={Uri.EscapeDataString(query)}&limit={perKind}", context.RequestAborted);
                // No Warehouse role: the user simply has no Warehouse group, screens included.
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return Results.StatusCode(403);
                if (response.IsSuccessStatusCode)
                {
                    var hits = await response.Content.ReadFromJsonAsync<List<SearchResultDto>>(context.RequestAborted) ?? [];
                    return Results.Ok(ShellSearchRelay.Rebase(hits, pathBase).Concat(screens));
                }

                loggers.CreateLogger(typeof(WarehouseShellSearch).FullName!).LogWarning(
                    "Warehouse search relay failed with status {StatusCode}", (int)response.StatusCode);
            }
            catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException
                || ex is OperationCanceledException && !context.RequestAborted.IsCancellationRequested)
            {
                loggers.CreateLogger(typeof(WarehouseShellSearch).FullName!).LogWarning(ex, "Warehouse search relay failed");
            }

            context.Response.Headers[SearchRequest.PartialHeader] = "1";
            return Results.Ok(screens);
        });
}
