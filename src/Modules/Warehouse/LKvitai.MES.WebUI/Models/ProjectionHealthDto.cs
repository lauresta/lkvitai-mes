namespace LKvitai.MES.WebUI.Models;

public record ProjectionHealthDto
{
    public double? LocationBalanceLag { get; init; }
    public double? AvailableStockLag { get; init; }
    public DateTime? LastRebuildLB { get; init; }
    public DateTime? LastRebuildAS { get; init; }

    // Backward-compatible fields for the current API payload shape.
    public string ProjectionName { get; init; } = string.Empty;
    public long? HighWaterMark { get; init; }
    public long? LastProcessed { get; init; }
    public double? LagSeconds { get; init; }
}
