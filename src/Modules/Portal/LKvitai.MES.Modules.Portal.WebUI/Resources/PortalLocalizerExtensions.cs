using System.Globalization;
using System.Resources;
using LKvitai.MES.Modules.Portal.WebUI.Models;
using Microsoft.Extensions.Localization;

namespace LKvitai.MES.Modules.Portal.WebUI.Resources;

/// <summary>
/// Localization helpers for Portal module tiles.
/// </summary>
/// <remarks>
/// Tile title / description / category come from the Portal API (seeded
/// defaults, editable via the Portal CMS). Seeded defaults are translated via
/// <c>Module.&lt;key&gt;.Title</c> / <c>Module.&lt;key&gt;.Description</c> /
/// <c>Module.Category.&lt;category&gt;</c>, but only while the API text still
/// equals the neutral (English) resource value — once an admin customises a
/// tile in the CMS, the edited text is shown as-is in every language.
/// </remarks>
public static class PortalLocalizerExtensions
{
    private static readonly ResourceManager NeutralResources = new(
        typeof(PortalResources).FullName!, typeof(PortalResources).Assembly);

    /// <summary>Localized module title, or the API text if customised / unknown.</summary>
    public static string ModuleTitle(this IStringLocalizer<PortalResources> localizer, ModuleCard card) =>
        Translate(localizer, $"Module.{card.Key}.Title", card.Title);

    /// <summary>Localized module description, or the API text if customised / unknown.</summary>
    public static string ModuleDescription(this IStringLocalizer<PortalResources> localizer, ModuleCard card) =>
        Translate(localizer, $"Module.{card.Key}.Description", card.Description);

    /// <summary>Localized module category, or the API text if unknown.</summary>
    public static string ModuleCategory(this IStringLocalizer<PortalResources> localizer, ModuleCard card) =>
        Translate(localizer, $"Module.Category.{card.Category}", card.Category);

    /// <summary>Localized lifecycle status label (Live / Pilot / Developing / Planned).</summary>
    public static string ModuleStatusLabel(this IStringLocalizer<PortalResources> localizer, ModuleStatus status) =>
        localizer[$"Common.Status.{status}"].Value;

    private static string Translate(IStringLocalizer<PortalResources> localizer, string key, string apiText)
    {
        var localized = localizer[key];
        if (localized.ResourceNotFound)
        {
            return apiText;
        }

        var neutral = NeutralResources.GetString(key, CultureInfo.InvariantCulture);
        return string.Equals(neutral, apiText, StringComparison.Ordinal) ? localized.Value : apiText;
    }
}
