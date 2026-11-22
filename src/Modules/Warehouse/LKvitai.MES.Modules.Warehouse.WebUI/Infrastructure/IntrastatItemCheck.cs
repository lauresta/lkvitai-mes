namespace LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;

/// <summary>Whether an item lacks the goods data an EU acquisition needs on the Intrastat declaration.</summary>
public static class IntrastatItemCheck
{
    /// <summary>True for a stock item with no commodity code, no net weight or no country of origin. Services never need it.</summary>
    public static bool IsDataMissing(string? itemType, string? commodityCode, decimal? netWeightKg, string? originCountry)
        => !string.Equals(itemType, "Service", StringComparison.OrdinalIgnoreCase)
           && (string.IsNullOrWhiteSpace(commodityCode)
               || netWeightKg is not > 0m
               || string.IsNullOrWhiteSpace(originCountry));
}
