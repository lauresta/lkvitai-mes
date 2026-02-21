namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public record AvailableStockItemDto
{
    public string WarehouseId { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string SKU { get; init; } = string.Empty;
    public decimal PhysicalQty { get; init; }
    public decimal ReservedQty { get; init; }
    public decimal AvailableQty { get; init; }
    public DateTime LastUpdated { get; init; }
}
