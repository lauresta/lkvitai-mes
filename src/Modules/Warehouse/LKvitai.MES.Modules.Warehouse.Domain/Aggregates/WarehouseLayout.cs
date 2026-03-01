namespace LKvitai.MES.Modules.Warehouse.Domain.Aggregates;

/// <summary>
/// State-based aggregate persisted directly by EF Core.
/// Public setters are intentional for this simple master-data entity.
/// </summary>
public class WarehouseLayout
{
    public Guid WarehouseId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsVirtual { get; set; }
    public string Status { get; set; } = "Active";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
