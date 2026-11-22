using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;

/// <summary>
/// Putaway offers physical storage cells only. Mirrors the server rule (PhysicalLocation.IsStorageLocation); the API
/// rejects anything else with a clear error, so this only keeps the select free of locations that would be refused.
/// </summary>
public static class PutawayDestinations
{
    private static readonly HashSet<string> NotStorage = new(StringComparer.OrdinalIgnoreCase)
    {
        "RECEIVING", "QC_HOLD", "QUARANTINE", "SHIPPING", "WAREHOUSE_MERGE",
        "SUPPLIER", "PRODUCTION", "SCRAP", "CUSTOMER", "RETURN_TO_SUPPLIER", "ADJUSTMENT", "SYSTEM", "AGNUM"
    };

    private const string InTransitPrefix = "IN_TRANSIT_";

    public static bool IsStorage(AdminLocationDto location)
        => !location.IsVirtual
           && !string.IsNullOrWhiteSpace(location.Code)
           && !NotStorage.Contains(location.Code.Trim())
           && !location.Code.StartsWith(InTransitPrefix, StringComparison.OrdinalIgnoreCase);
}
