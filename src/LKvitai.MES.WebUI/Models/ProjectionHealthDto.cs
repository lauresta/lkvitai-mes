namespace LKvitai.MES.WebUI.Models;

public record ProjectionHealthDto
{
    public string ProjectionName { get; init; } = string.Empty;
    public long? HighWaterMark { get; init; }
    public long? LastProcessed { get; init; }
    public double? LagSeconds { get; init; }
}
