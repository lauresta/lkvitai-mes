namespace LKvitai.MES.WebUI.Models;

public record HealthStatusDto
{
    public bool Ok { get; init; }
    public string Service { get; init; } = string.Empty;
    public string Version { get; init; } = "dev";
    public DateTime UtcNow { get; init; }
}
