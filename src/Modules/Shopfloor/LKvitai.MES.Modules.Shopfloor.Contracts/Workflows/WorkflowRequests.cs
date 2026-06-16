namespace LKvitai.MES.Modules.Shopfloor.Contracts.Workflows;

/// <summary>
/// Create a workflow template. The server generates the default graph
/// (one start + one finish joined by an edge) and sets status to Draft;
/// the client does not send a graph on create.
/// </summary>
public sealed record CreateWorkflowTemplateRequest(
    string Code,
    string Name,
    string? Description);

/// <summary>Update a workflow template's header fields.</summary>
public sealed record UpdateWorkflowTemplateRequest(
    string Code,
    string Name,
    string? Description);

/// <summary>Save a (possibly in-progress) workflow graph.</summary>
public sealed record SaveWorkflowGraphRequest(WorkflowGraphDto Graph);

/// <summary>Clone an existing workflow template into a new Draft.</summary>
public sealed record CloneWorkflowTemplateRequest(
    string Code,
    string Name,
    string? Description);
