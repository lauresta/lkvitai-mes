namespace LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure.ItemCard;

/// <summary>Tabs of the item card and their <c>?tab=</c> slugs; the default tab has no query value.</summary>
public static class ItemCardTabs
{
    public const string Locations = "vietos";
    public const string Movements = "judejimai";
    public const string Prices = "kainos";
    public const string Photos = "nuotraukos";
    public const string Data = "duomenys";

    public static readonly IReadOnlyList<string> All = [Locations, Movements, Prices, Photos, Data];

    public static string Parse(string? value)
    {
        var trimmed = value?.Trim();
        return All.FirstOrDefault(x => string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase)) ?? Locations;
    }

    /// <summary>Query value for a tab; null removes the parameter for the default tab.</summary>
    public static string? ToQuery(string tab)
        => string.Equals(tab, Locations, StringComparison.Ordinal) ? null : Parse(tab);

    public static string LabelKey(string tab)
        => tab switch
        {
            Movements => "AdminItemDetail.Tab.Movements",
            Prices => "AdminItemDetail.Tab.PricesSuppliers",
            Photos => "AdminItemDetail.Tab.Photos",
            Data => "AdminItemDetail.Tab.Data",
            _ => "AdminItemDetail.Tab.Locations"
        };
}
