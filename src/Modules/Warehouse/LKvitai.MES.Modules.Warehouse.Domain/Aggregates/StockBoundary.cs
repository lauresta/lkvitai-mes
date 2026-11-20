namespace LKvitai.MES.Modules.Warehouse.Domain.Aggregates;

/// <summary>
/// Stock boundary for value accounting (blueprint 4.1). Inside: physical cells and the virtual codes
/// RECEIVING, QC_HOLD, QUARANTINE, SHIPPING, IN_TRANSIT_*, WAREHOUSE_MERGE. Outside: SUPPLIER, PRODUCTION,
/// SCRAP, CUSTOMER, RETURN_TO_SUPPLIER, ADJUSTMENT, SYSTEM, AGNUM. A movement across the boundary changes
/// value; a movement inside it does not.
/// </summary>
public static class StockBoundary
{
    private static readonly HashSet<string> Outside = new(StringComparer.OrdinalIgnoreCase)
    {
        "SUPPLIER", "PRODUCTION", "SCRAP", "CUSTOMER", "RETURN_TO_SUPPLIER", "ADJUSTMENT", "SYSTEM", "AGNUM"
    };

    private static readonly HashSet<string> InsideVirtual = new(StringComparer.OrdinalIgnoreCase)
    {
        "RECEIVING", "QC_HOLD", "QUARANTINE", "SHIPPING", "WAREHOUSE_MERGE"
    };

    public const string InTransitPrefix = "IN_TRANSIT_";

    public static bool IsOutside(string? locationCode)
        => !string.IsNullOrWhiteSpace(locationCode) && Outside.Contains(locationCode.Trim());

    /// <summary>Anything that is not on the outside list lies on the territory (physical cells included).</summary>
    public static bool IsInside(string? locationCode)
        => !string.IsNullOrWhiteSpace(locationCode) && !IsOutside(locationCode);

    public static bool IsKnownVirtual(string? locationCode)
        => !string.IsNullOrWhiteSpace(locationCode) &&
           (Outside.Contains(locationCode.Trim()) || InsideVirtual.Contains(locationCode.Trim()) ||
            locationCode.Trim().StartsWith(InTransitPrefix, StringComparison.OrdinalIgnoreCase));
}
