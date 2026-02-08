namespace LKvitai.MES.WebUI.Models;

public record HealthStatusDto
{
    public string Status { get; init; } = string.Empty;
    public double ProjectionLag { get; init; }
    public DateTime LastCheck { get; init; }
}
