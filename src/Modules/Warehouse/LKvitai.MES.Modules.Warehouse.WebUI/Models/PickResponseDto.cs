namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public record PickResponseDto
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}
