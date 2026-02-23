namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public sealed record DRDrillDto(
    Guid Id,
    DateTimeOffset DrillStartedAt,
    DateTimeOffset? DrillCompletedAt,
    string Scenario,
    TimeSpan ActualRTO,
    string Status,
    string Notes,
    IReadOnlyList<string> IssuesIdentified);

public sealed class TriggerDrillRequestDto
{
    public string Scenario { get; set; } = "DATA_CENTER_OUTAGE";
}
