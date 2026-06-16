namespace LKvitai.MES.Modules.Shopfloor.Contracts.Workflows;

/// <summary>
/// Row shape for the Production Families list. Carries the derived
/// <see cref="MappedLegacyCount"/> and <see cref="TaskCount"/> so the list can
/// render without per-row follow-up calls.
/// </summary>
public sealed record WorkflowTemplateSummaryDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    WorkflowStatus Status,
    int MappedLegacyCount,
    int TaskCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>Full workflow template, including the authored graph.</summary>
public sealed record WorkflowTemplateDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    WorkflowStatus Status,
    WorkflowGraphDto Graph,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
