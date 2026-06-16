namespace LKvitai.MES.Modules.Shopfloor.Domain.Entities;

/// <summary>
/// Maps a legacy product type to a workflow template. Kept in a separate table
/// so legacy re-sync never drops mappings.
/// </summary>
public sealed class ProductTypeWorkflowMap
{
    public string LegacyProductTypeCode { get; private set; } = string.Empty;
    public Guid WorkflowTemplateId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    private ProductTypeWorkflowMap() { }

    public ProductTypeWorkflowMap(string legacyProductTypeCode, Guid workflowTemplateId, DateTimeOffset createdAt)
    {
        LegacyProductTypeCode = string.IsNullOrWhiteSpace(legacyProductTypeCode)
            ? throw new ArgumentException("legacyProductTypeCode is required.", nameof(legacyProductTypeCode))
            : legacyProductTypeCode.Trim();
        WorkflowTemplateId = workflowTemplateId == Guid.Empty
            ? throw new ArgumentException("workflowTemplateId is required.", nameof(workflowTemplateId))
            : workflowTemplateId;
        CreatedAt = createdAt;
    }

    public void Reassign(Guid workflowTemplateId, DateTimeOffset now)
    {
        WorkflowTemplateId = workflowTemplateId == Guid.Empty
            ? throw new ArgumentException("workflowTemplateId is required.", nameof(workflowTemplateId))
            : workflowTemplateId;
        UpdatedAt = now;
    }
}
