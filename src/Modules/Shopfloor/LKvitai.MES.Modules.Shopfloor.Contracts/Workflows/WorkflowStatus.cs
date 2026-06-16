namespace LKvitai.MES.Modules.Shopfloor.Contracts.Workflows;

/// <summary>
/// Lifecycle state of a workflow template (production family). The persisted
/// shape allows <see cref="Archived"/>, but the MVP authoring UI only exposes
/// <see cref="Draft"/> and <see cref="Published"/>.
/// </summary>
public enum WorkflowStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2,
}
