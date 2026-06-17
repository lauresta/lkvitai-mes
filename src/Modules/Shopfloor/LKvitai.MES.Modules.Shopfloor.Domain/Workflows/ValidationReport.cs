namespace LKvitai.MES.Modules.Shopfloor.Domain.Workflows;

/// <summary>Smart-validation severity tiers (see shopfloor-12 §2).</summary>
public enum ValidationSeverity
{
    /// <summary>Flow is structurally invalid / unrunnable — blocks publish.</summary>
    Error,

    /// <summary>Flow runs, but has a production problem (queue, bottleneck, imbalance).</summary>
    Warning,

    /// <summary>Hygiene / optimization suggestion.</summary>
    Hint,
}

/// <summary>
/// Read-only metadata about a work station, supplied to the validator so
/// station-aware rules (W3/W5, H3) and labels can be computed without the
/// Domain layer depending on persistence.
/// </summary>
public sealed record WorkflowStationInfo(Guid Id, string Code, string Name, int? WipLimit, bool IsActive);

/// <summary>What a finding points at on the canvas (highlight targets).</summary>
public sealed record ValidationTargets(
    IReadOnlyList<string> NodeIds,
    IReadOnlyList<string> EdgeIds,
    IReadOnlyList<string> StationIds)
{
    public static readonly ValidationTargets None =
        new(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

    public static ValidationTargets Nodes(params string[] nodeIds) =>
        new(nodeIds, Array.Empty<string>(), Array.Empty<string>());

    /// <summary>Canonical edge identifier used in <see cref="EdgeIds"/>.</summary>
    public static string EdgeId(string from, string to) => $"{from}->{to}";
}

/// <summary>A single validation finding (one rule firing on one target set).</summary>
public sealed record ValidationFinding(
    string RuleId,
    ValidationSeverity Severity,
    string Title,
    string Message,
    ValidationTargets Targets,
    IReadOnlyDictionary<string, double>? Detail = null);

public sealed record CriticalPathInfo(IReadOnlyList<string> NodeIds, int DurationSec);

public sealed record BottleneckInfo(string StationId, string StationName, int LoadSec);

public sealed record LineLoadInfo(string StationId, string StationName, int LoadSec, int TaskCount);

public sealed record MergeInfo(string NodeId, int MaxInSec, int MinInSec, int WaitSec);

/// <summary>Numbers the report surfaces beyond pass/fail (shopfloor-12 §4).</summary>
public sealed record ValidationMetrics(
    int LeadTimeSec,
    CriticalPathInfo? CriticalPath,
    BottleneckInfo? Bottleneck,
    int ThroughputPerHour,
    IReadOnlyList<LineLoadInfo> LineLoads,
    IReadOnlyList<MergeInfo> Merges)
{
    public static readonly ValidationMetrics Empty =
        new(0, null, null, 0, Array.Empty<LineLoadInfo>(), Array.Empty<MergeInfo>());
}

public sealed record ValidationSummary(int Errors, int Warnings, int Hints);

/// <summary>
/// One shape produced by the engine and consumed by both the editor report
/// panel and (on publish) the API (shopfloor-12 §5).
/// </summary>
public sealed record ValidationReport(
    int Score,
    bool Publishable,
    ValidationSummary Summary,
    ValidationMetrics Metrics,
    IReadOnlyList<ValidationFinding> Findings);
