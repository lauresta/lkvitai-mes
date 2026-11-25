using LKvitai.MES.Modules.Warehouse.WebUI.Resources;
using Microsoft.Extensions.Localization;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure.ItemCard;

/// <summary>Localised texts shared by the item card components.</summary>
public static class ItemCardErrors
{
    /// <summary>Actor label: "Sistema" for system ids and hashes, a short tail for other opaque ids.</summary>
    public static string Actor(IStringLocalizer<WarehouseResources> localizer, string? actor)
        => ItemCardText.Actor(
            actor,
            localizer["AdminItemDetail.System"].Value,
            tail => localizer["Valuation.Dashboard.UnknownUser", tail].Value);

    /// <summary>Localised text of an API error, with the problem detail appended when the API sent one.</summary>
    public static string Describe(IStringLocalizer<WarehouseResources> localizer, ApiException error)
    {
        var text = WarehouseErrorText.Get(localizer, error);
        var detail = error.ProblemDetails?.Detail;
        return string.IsNullOrWhiteSpace(detail) ? text : $"{text} {detail}";
    }
}
