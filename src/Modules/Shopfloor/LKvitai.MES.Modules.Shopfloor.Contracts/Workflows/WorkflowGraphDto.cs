namespace LKvitai.MES.Modules.Shopfloor.Contracts.Workflows;

/// <summary>
/// Serializable workflow graph: the node/edge model authored in the workflow
/// editor and persisted as <c>workflow_templates.graph_json</c>.
/// </summary>
public sealed record WorkflowGraphDto(
    IReadOnlyList<WorkflowNodeDto> Nodes,
    IReadOnlyList<WorkflowEdgeDto> Edges);

/// <summary>
/// A single graph node. Only <c>task</c> nodes use <see cref="WorkStationId"/>,
/// <see cref="DurationSec"/> and the optional <see cref="TaskTypeCode"/>.
/// </summary>
public sealed record WorkflowNodeDto(
    string Id,
    string Kind,
    string Name,
    WorkflowNodePositionDto Position,
    Guid? WorkStationId,
    int? DurationSec,
    string? TaskTypeCode);

public sealed record WorkflowEdgeDto(string From, string To);

public sealed record WorkflowNodePositionDto(decimal X, decimal Y);

/// <summary>Allowed values for <see cref="WorkflowNodeDto.Kind"/>.</summary>
public static class WorkflowNodeKinds
{
    public const string Start = "start";
    public const string Task = "task";
    public const string Finish = "finish";
}
