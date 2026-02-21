namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public record AvailableStockSearchFilters
{
    public string? Warehouse { get; init; }
    public string Location { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public bool IncludeVirtual { get; init; }

    public bool HasAtLeastOneFilter =>
        !string.IsNullOrWhiteSpace(Warehouse) ||
        !string.IsNullOrWhiteSpace(Location) ||
        !string.IsNullOrWhiteSpace(Sku);
}
