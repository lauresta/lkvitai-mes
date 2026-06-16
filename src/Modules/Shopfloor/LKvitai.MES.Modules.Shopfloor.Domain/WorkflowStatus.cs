namespace LKvitai.MES.Modules.Shopfloor.Domain;

/// <summary>
/// Persisted lifecycle state of a workflow template. <see cref="Archived"/> is
/// reserved — the persisted shape allows it, but the MVP UI only exposes Draft
/// and Published.
/// </summary>
public enum WorkflowStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2,
}
