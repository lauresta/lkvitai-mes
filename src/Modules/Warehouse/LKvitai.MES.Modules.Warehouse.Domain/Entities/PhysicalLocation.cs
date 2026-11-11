namespace LKvitai.MES.Modules.Warehouse.Domain.Entities;

/// <summary>Shared physical-stock boundary; virtual and legacy snapshot locations never qualify.</summary>
public static class PhysicalLocation
{
    /// <summary>Virtual transit cell used only by the one-off warehouse consolidation.</summary>
    public const string WarehouseMergeCode = "WAREHOUSE_MERGE";

    public static bool IsPhysical(Location? location) =>
        location is { IsVirtual: false } && IsPhysicalCode(location.Code);

    public static bool IsPhysicalCode(string? code) =>
        !string.IsNullOrWhiteSpace(code) &&
        !code.Equals("AGNUM", StringComparison.OrdinalIgnoreCase) &&
        !code.Equals(WarehouseMergeCode, StringComparison.OrdinalIgnoreCase) &&
        !code.StartsWith("IN_TRANSIT_", StringComparison.OrdinalIgnoreCase);
}
