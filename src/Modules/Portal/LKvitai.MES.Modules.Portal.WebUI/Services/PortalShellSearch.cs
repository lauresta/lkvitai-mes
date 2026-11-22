using LKvitai.MES.BuildingBlocks.Search;
using LKvitai.MES.BuildingBlocks.WebUI.Localization;
using LKvitai.MES.Modules.Portal.WebUI.Models;
using LKvitai.MES.Modules.Portal.WebUI.Resources;
using Microsoft.Extensions.Localization;

namespace LKvitai.MES.Modules.Portal.WebUI.Services;

/// <summary>
/// Portal leg of the portal "Search everything" box (issue #375): the Portal's own pages and the module
/// tiles of the home page, so typing a module name opens that module.
/// </summary>
public static class PortalShellSearch
{
    public static async Task<IReadOnlyList<ScreenEntry>> ScreensAsync(HttpContext context)
    {
        var text = context.RequestServices.GetRequiredService<IStringLocalizer<PortalResources>>();
        var portal = text["Common.Portal"].Value;
        var screens = new List<ScreenEntry>
        {
            ScreenEntry.FromResource(text, string.Empty, "Common.Portal", text["Common.Modules"].Value, CikadaLocalization.LanguageCodes),
            ScreenEntry.FromResource(text, "cms/tiles", "Common.Tiles", text["Common.PortalCms"].Value, CikadaLocalization.LanguageCodes),
        };

        var tiles = await TilesAsync(context);
        screens.AddRange(Tiles(tiles, text, portal));
        return screens;
    }

    /// <summary>
    /// Navigable tiles as rooted screens. A tile that is planned, hidden, or does not point at a path
    /// on this host is not a search result.
    /// </summary>
    public static IEnumerable<ScreenEntry> Tiles(IEnumerable<ModuleCard> tiles, IStringLocalizer<PortalResources> text, string portal)
        => tiles
            .Where(tile => tile.IsVisible && tile.Status is not ModuleStatus.Planned && IsLocalPath(tile.Url))
            .Select(tile => new ScreenEntry(
                tile.Url!,
                text.ModuleTitle(tile),
                $"{portal} · {text.ModuleCategory(tile)}",
                ScreenLabels.InCultures(() => text.ModuleTitle(tile), CikadaLocalization.LanguageCodes),
                Rooted: true));

    private static bool IsLocalPath(string? url)
        => !string.IsNullOrWhiteSpace(url) && url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal);

    private static async Task<IReadOnlyList<ModuleCard>> TilesAsync(HttpContext context)
    {
        try
        {
            return await context.RequestServices.GetRequiredService<PortalApiClient>().GetModulesAsync(context.RequestAborted);
        }
        catch (InvalidOperationException)
        {
            // No bearer token in this session for the Portal API: the Portal pages are still searchable.
            return [];
        }
    }
}
