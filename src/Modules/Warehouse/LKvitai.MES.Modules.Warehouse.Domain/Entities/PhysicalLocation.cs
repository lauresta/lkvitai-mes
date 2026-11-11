namespace LKvitai.MES.Modules.Warehouse.Domain.Entities;

/// <summary>Shared physical-stock boundary; virtual and legacy snapshot locations never qualify.</summary>
public static class PhysicalLocation
{
    public static bool IsPhysical(Location? location) =>
        location is { IsVirtual: false } && IsPhysicalCode(location.Code);

    public static bool IsPhysicalCode(string? code) =>
        !string.IsNullOrWhiteSpace(code) &&
        !code.Equals("AGNUM", StringComparison.OrdinalIgnoreCase) &&
        !code.StartsWith("IN_TRANSIT_", StringComparison.OrdinalIgnoreCase);
}
