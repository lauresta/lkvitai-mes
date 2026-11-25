using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

/// <summary>
/// Role of a stock row's location as the item card shows it. The virtual codes inside the stock boundary get a
/// fixed role (so receiving stock can be flagged as "awaiting put-away"), codes outside the boundary are
/// <see cref="Outside"/>, and real cells keep the role configured on the location (default <see cref="Storage"/>).
/// </summary>
public static class StockLocationRole
{
    public const string Receiving = "Receiving";
    public const string QcHold = "QcHold";
    public const string Quarantine = "Quarantine";
    public const string Shipping = "Shipping";
    public const string Transit = "Transit";
    public const string Outside = "Outside";
    public const string Storage = "Storage";

    private static readonly Dictionary<string, string> VirtualRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RECEIVING"] = Receiving,
        ["QC_HOLD"] = QcHold,
        ["QUARANTINE"] = Quarantine,
        ["SHIPPING"] = Shipping,
        [PhysicalLocation.WarehouseMergeCode] = Transit
    };

    /// <summary>Null when the code is empty or the location is unknown and not one of the fixed virtual codes.</summary>
    public static string? Resolve(string? locationCode, Location? location)
    {
        var code = locationCode?.Trim();
        if (string.IsNullOrEmpty(code))
        {
            return null;
        }

        if (VirtualRoles.TryGetValue(code, out var role))
        {
            return role;
        }

        if (code.StartsWith(StockBoundary.InTransitPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Transit;
        }

        if (StockBoundary.IsOutside(code))
        {
            return Outside;
        }

        if (location is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(location.LocationRole) ? Storage : location.LocationRole.Trim();
    }
}
