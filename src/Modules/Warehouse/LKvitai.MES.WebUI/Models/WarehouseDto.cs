namespace LKvitai.MES.WebUI.Models;

public record WarehouseDto
{
    public string Id { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}
