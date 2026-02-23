namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public sealed record LotListItemDto
{
    public int Id { get; init; }
    public int ItemId { get; init; }
    public string ItemSku { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public string LotNumber { get; init; } = string.Empty;
    public DateOnly? ProductionDate { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    public decimal Qty { get; init; }
    public decimal ReservedQty { get; init; }
    public decimal AvailableQty { get; init; }
    public string BaseUom { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}
