namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public record ProjectionLagStatusDto
{
    public string ProjectionName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public double? LagSeconds { get; init; }
    public long? LagEvents { get; init; }
    public DateTimeOffset? LastUpdated { get; init; }
}
