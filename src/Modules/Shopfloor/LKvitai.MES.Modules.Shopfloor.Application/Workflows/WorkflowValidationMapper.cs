using LKvitai.MES.Modules.Shopfloor.Contracts.Workflows;
using LKvitai.MES.Modules.Shopfloor.Domain.Workflows;

namespace LKvitai.MES.Modules.Shopfloor.Application.Workflows;

/// <summary>Maps the domain <see cref="ValidationReport"/> to its serializable DTO.</summary>
public static class WorkflowValidationMapper
{
    public static ValidationReportDto ToDto(ValidationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new ValidationReportDto(
            report.Score,
            report.Publishable,
            new ValidationSummaryDto(report.Summary.Errors, report.Summary.Warnings, report.Summary.Hints),
            ToDto(report.Metrics),
            report.Findings.Select(ToDto).ToList());
    }

    private static ValidationMetricsDto ToDto(ValidationMetrics m) => new(
        m.LeadTimeSec,
        m.CriticalPath is null ? null : new CriticalPathDto(m.CriticalPath.NodeIds, m.CriticalPath.DurationSec),
        m.Bottleneck is null ? null : new BottleneckDto(m.Bottleneck.StationId, m.Bottleneck.StationName, m.Bottleneck.LoadSec),
        m.ThroughputPerHour,
        m.LineLoads.Select(l => new LineLoadDto(l.StationId, l.StationName, l.LoadSec, l.TaskCount)).ToList(),
        m.Merges.Select(g => new MergeDto(g.NodeId, g.MaxInSec, g.MinInSec, g.WaitSec)).ToList());

    private static ValidationFindingDto ToDto(ValidationFinding f) => new(
        f.RuleId,
        SeverityName(f.Severity),
        f.Title,
        f.Message,
        new ValidationTargetsDto(f.Targets.NodeIds, f.Targets.EdgeIds, f.Targets.StationIds),
        f.Detail);

    private static string SeverityName(ValidationSeverity severity) => severity switch
    {
        ValidationSeverity.Error => "error",
        ValidationSeverity.Warning => "warning",
        ValidationSeverity.Hint => "hint",
        _ => "hint",
    };
}
