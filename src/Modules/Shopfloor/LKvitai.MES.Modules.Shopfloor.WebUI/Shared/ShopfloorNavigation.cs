using LKvitai.MES.BuildingBlocks.Search;
using LKvitai.MES.BuildingBlocks.WebUI.Localization;
using LKvitai.MES.Modules.Shopfloor.WebUI.Resources;
using Microsoft.Extensions.Localization;

namespace LKvitai.MES.Modules.Shopfloor.WebUI.Shared;

/// <summary>
/// The Shopfloor sidebar as data: sections and screens with their resource keys. <c>NavMenu</c> renders it
/// and the portal "Search everything" box (issue #375) searches it, so a screen added here shows up in both.
/// </summary>
public static class ShopfloorNavigation
{
    public sealed record NavItem(string Href, string Icon, string Label);

    public sealed record NavSection(string Key, string Label, string Icon, NavItem[] Items);

    /// <summary>The dashboard link rendered above the sections.</summary>
    public static NavItem Dashboard { get; } = new("dashboard", "bi-speedometer2", "Shopfloor.Dashboard");

    public static IReadOnlyList<NavSection> Sections { get; } =
    [
        new("workflows", "Shopfloor.Workflows", "bi-diagram-3", [
            new("workflows", "bi-diagram-2", "Shopfloor.ProductionFamilies"),
            new("workflow-editor", "bi-diagram-3", "Shopfloor.WorkflowEditor"),
        ]),
        new("reference", "Shopfloor.ReferenceData", "bi-grid-3x3-gap", [
            new("ref/work-centers", "bi-buildings", "Shopfloor.WorkCenters"),
            new("ref/stations", "bi-pc-display", "Shopfloor.WorkStations"),
        ]),
        new("mappings", "Shopfloor.Mappings", "bi-link-45deg", [
            new("map/product-types", "bi-tags", "Shopfloor.LegacyTypesCoverage"),
        ]),
        new("execution", "Shopfloor.Execution", "bi-kanban", [
            new("gamyba", "bi-tools", "Shopfloor.Gamyba"),
            new("operator-work-queue", "bi-list-check", "Shopfloor.OperatorQueue"),
            new("roletai", "bi-file-earmark-check", "Roletai.Paperless.Nav"),
        ]),
    ];

    /// <summary>Every screen of the sidebar as a search entry: label in the current UI language, matched in all of them.</summary>
    public static IReadOnlyList<ScreenEntry> Screens(IStringLocalizer<ShopfloorResources> text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var module = text["Shopfloor.Shopfloor"].Value;
        var screens = new List<ScreenEntry>
        {
            ScreenEntry.FromResource(text, Dashboard.Href, Dashboard.Label, module, CikadaLocalization.LanguageCodes),
        };

        foreach (var section in Sections)
        {
            var subtitle = $"{module} · {text[section.Label].Value}";
            screens.AddRange(section.Items.Select(item =>
                ScreenEntry.FromResource(text, item.Href, item.Label, subtitle, CikadaLocalization.LanguageCodes)));
        }

        return screens;
    }
}
