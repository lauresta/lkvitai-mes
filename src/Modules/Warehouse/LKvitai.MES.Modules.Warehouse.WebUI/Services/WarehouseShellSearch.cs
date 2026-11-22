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
/// "WarehouseApi" client every page uses (its handler attaches the token), and adds the screens of
/// the Warehouse sidebar.
/// </summary>
public static class WarehouseShellSearch
{
    public const string HttpClientName = "WarehouseApi";
    public const string ApiPath = "/api/warehouse/v1/search";

    public static IEndpointConventionBuilder MapWarehouseShellSearch(this IEndpointRouteBuilder app)
        => app.MapGet("/shell/search", async (
            HttpContext context, string? q, int? limit, IHttpClientFactory clients,
            IStringLocalizer<WarehouseResources> text, ILoggerFactory loggers) =>
        {
            var authenticated = context.User.Identity?.IsAuthenticated == true;
            if (ShellSearchRelay.Reject(context, authenticated, q, limit, out var input) is { } rejected) return rejected;

            var screens = ScreenSearch.Match(WarehouseNavigation.Screens(text), input!.Query, input.Limit, input.PathBase);
            return await ShellSearchRelay.RelayAsync(context, input, screens, clients.CreateClient(HttpClientName),
                ApiPath, bearer: null, loggers.CreateLogger(typeof(WarehouseShellSearch).FullName!));
        });
}
