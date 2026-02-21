namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public record RecentMovementDto
{
    public Guid MovementId { get; init; }
    public string SKU { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string FromLocation { get; init; } = string.Empty;
    public string ToLocation { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}
