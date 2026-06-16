namespace LKvitai.MES.Modules.Shopfloor.Contracts.Legacy;

/// <summary>
/// A legacy product type row plus its current mapping (if any), so the list
/// can render the family chip without a second call. The
/// <c>GET /legacy-product-types</c> query LEFT-JOINs the mapping +
/// workflow_templates tables to populate the <c>Mapped*</c> fields.
/// </summary>
public sealed record LegacyProductTypeDto(
    string Code,
    string KindName,
    string Name,
    bool Removed,
    Guid? MappedWorkflowTemplateId,
    string? MappedWorkflowCode,
    string? MappedWorkflowName);
