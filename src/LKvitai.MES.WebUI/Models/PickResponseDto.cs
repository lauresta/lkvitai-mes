namespace LKvitai.MES.WebUI.Models;

public record PickResponseDto
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}
