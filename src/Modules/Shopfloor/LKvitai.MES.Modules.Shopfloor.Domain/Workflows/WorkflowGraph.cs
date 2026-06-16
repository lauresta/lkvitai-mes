namespace LKvitai.MES.Modules.Shopfloor.Domain.Workflows;

/// <summary>Allowed node kinds in a workflow graph.</summary>
public enum WorkflowNodeKind
{
    Start,
    Task,
    Finish,
}

/// <summary>A single node in a workflow graph (domain-level, framework-free).</summary>
public sealed record WorkflowGraphNode(
    string Id,
    WorkflowNodeKind Kind,
    string Name,
    Guid? WorkStationId,
    int? DurationSec,
    string? TaskTypeCode);

public sealed record WorkflowGraphEdge(string From, string To);

/// <summary>Domain-level workflow graph used by <see cref="WorkflowGraphValidator"/>.</summary>
public sealed record WorkflowGraph(
    IReadOnlyList<WorkflowGraphNode> Nodes,
    IReadOnlyList<WorkflowGraphEdge> Edges);
