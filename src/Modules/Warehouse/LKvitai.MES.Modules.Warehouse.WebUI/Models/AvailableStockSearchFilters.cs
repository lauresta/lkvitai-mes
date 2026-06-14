namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public record AvailableStockSearchFilters
{
    public string? Warehouse { get; init; }
    public string Location { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public string Item { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public string Tag { get; init; } = string.Empty;
    public string Supplier { get; init; } = string.Empty;
    public string SupplierCountry { get; init; } = string.Empty;
    public bool IncludeVirtual { get; init; }

    public bool HasAtLeastOneFilter =>
        !string.IsNullOrWhiteSpace(Warehouse) ||
        !string.IsNullOrWhiteSpace(Location) ||
        !string.IsNullOrWhiteSpace(Sku) ||
        !string.IsNullOrWhiteSpace(Item) ||
        !string.IsNullOrWhiteSpace(ItemName) ||
        !string.IsNullOrWhiteSpace(Tag) ||
        !string.IsNullOrWhiteSpace(Supplier) ||
        !string.IsNullOrWhiteSpace(SupplierCountry);
}
