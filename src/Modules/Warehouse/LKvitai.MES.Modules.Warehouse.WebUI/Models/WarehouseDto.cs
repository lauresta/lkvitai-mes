namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public record WarehouseDto
{
    public string Id { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsVirtual { get; init; }
    public string Status { get; init; } = string.Empty;
}
