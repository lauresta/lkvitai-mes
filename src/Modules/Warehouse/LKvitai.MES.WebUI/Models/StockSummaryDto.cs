namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public record StockSummaryDto
{
    public int TotalSKUs { get; init; }
    public decimal TotalQuantity { get; init; }
    public decimal TotalValue { get; init; }
}
