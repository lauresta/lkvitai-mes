namespace LKvitai.MES.Api.Observability;

public sealed class SlaMonitoringOptions
{
    public const string SectionName = "SlaMonitoring";

    public int RequestWindowSize { get; set; } = 5000;

    public int TrackingWindowDays { get; set; } = 30;

    public double UptimeTargetPercent { get; set; } = 99.9;

    public double ApiP95TargetMs { get; set; } = 500;

    public double ProjectionLagTargetSeconds { get; set; } = 1;

    public double OrderFulfillmentTargetRate { get; set; } = 0.95;

    public double PlannedDowntimeMinutes { get; set; }
}
