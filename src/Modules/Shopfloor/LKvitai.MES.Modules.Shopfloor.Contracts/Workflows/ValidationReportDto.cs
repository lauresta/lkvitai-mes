namespace LKvitai.MES.Modules.Shopfloor.Contracts.Workflows;

/// <summary>
/// Serializable smart-validation report (shopfloor-12 §5). One shape consumed by
/// the editor report panel and (on publish) the API. <c>Severity</c> is the
/// lowercase string <c>"error" | "warning" | "hint"</c> to match the editor.
/// </summary>
public sealed record ValidationReportDto(
    int Score,
    bool Publishable,
    ValidationSummaryDto Summary,
    ValidationMetricsDto Metrics,
    IReadOnlyList<ValidationFindingDto> Findings);

public sealed record ValidationSummaryDto(int Errors, int Warnings, int Hints);

public sealed record ValidationFindingDto(
    string RuleId,
    string Severity,
    string Title,
    string Message,
    ValidationTargetsDto Targets,
    IReadOnlyDictionary<string, double>? Detail);

public sealed record ValidationTargetsDto(
    IReadOnlyList<string> NodeIds,
    IReadOnlyList<string> EdgeIds,
    IReadOnlyList<string> StationIds);

public sealed record ValidationMetricsDto(
    int LeadTimeSec,
    CriticalPathDto? CriticalPath,
    BottleneckDto? Bottleneck,
    int ThroughputPerHour,
    IReadOnlyList<LineLoadDto> LineLoads,
    IReadOnlyList<MergeDto> Merges);

public sealed record CriticalPathDto(IReadOnlyList<string> NodeIds, int DurationSec);

public sealed record BottleneckDto(string StationId, string StationName, int LoadSec);

public sealed record LineLoadDto(string StationId, string StationName, int LoadSec, int TaskCount);

public sealed record MergeDto(string NodeId, int MaxInSec, int MinInSec, int WaitSec);
