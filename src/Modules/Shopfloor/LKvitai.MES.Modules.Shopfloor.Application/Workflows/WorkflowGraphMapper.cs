using System.Text.Json;
using LKvitai.MES.Modules.Shopfloor.Application.Exceptions;
using LKvitai.MES.Modules.Shopfloor.Contracts.Workflows;
using LKvitai.MES.Modules.Shopfloor.Domain.Workflows;

namespace LKvitai.MES.Modules.Shopfloor.Application.Workflows;

/// <summary>
/// Maps between the serializable <see cref="WorkflowGraphDto"/> (what is stored
/// in <c>graph_json</c> and exchanged with the editor) and the domain-level
/// <see cref="WorkflowGraph"/> consumed by the validator.
/// </summary>
public static class WorkflowGraphMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string Serialize(WorkflowGraphDto graph)
        => JsonSerializer.Serialize(graph, JsonOptions);

    public static WorkflowGraphDto Deserialize(string graphJson)
    {
        if (string.IsNullOrWhiteSpace(graphJson))
        {
            throw new ShopfloorValidationException("Workflow graph JSON is empty.");
        }

        try
        {
            var graph = JsonSerializer.Deserialize<WorkflowGraphDto>(graphJson, JsonOptions);
            if (graph is null)
            {
                throw new ShopfloorValidationException("Workflow graph JSON is null.");
            }

            return new WorkflowGraphDto(
                graph.Nodes ?? Array.Empty<WorkflowNodeDto>(),
                graph.Edges ?? Array.Empty<WorkflowEdgeDto>());
        }
        catch (JsonException ex)
        {
            throw new ShopfloorValidationException($"Workflow graph JSON is invalid: {ex.Message}");
        }
    }

    /// <summary>
    /// Maps the DTO to the domain graph. Unknown node kinds are rejected here
    /// (part of the lenient ruleset: "known node kinds").
    /// </summary>
    public static WorkflowGraph ToDomain(WorkflowGraphDto graph)
    {
        var nodes = new List<WorkflowGraphNode>(graph.Nodes.Count);
        foreach (var node in graph.Nodes)
        {
            nodes.Add(new WorkflowGraphNode(
                node.Id,
                ParseKind(node.Kind),
                node.Name,
                node.WorkStationId,
                node.DurationSec,
                node.TaskTypeCode,
                node.Description));
        }

        var edges = graph.Edges
            .Select(e => new WorkflowGraphEdge(e.From, e.To))
            .ToList();

        return new WorkflowGraph(nodes, edges);
    }

    private static WorkflowNodeKind ParseKind(string kind) => kind switch
    {
        WorkflowNodeKinds.Start => WorkflowNodeKind.Start,
        WorkflowNodeKinds.Task => WorkflowNodeKind.Task,
        WorkflowNodeKinds.Finish => WorkflowNodeKind.Finish,
        _ => throw new ShopfloorValidationException($"Unknown node kind '{kind}'."),
    };

    /// <summary>
    /// Minimal valid graph seeded on create: one start and one finish joined by
    /// a single edge, so a brand-new draft is immediately valid.
    /// </summary>
    public static WorkflowGraphDto DefaultGraph() => new(
        Nodes: new[]
        {
            new WorkflowNodeDto("start", WorkflowNodeKinds.Start, "Job received",
                new WorkflowNodePositionDto(120m, 200m), null, null, null),
            new WorkflowNodeDto("finish", WorkflowNodeKinds.Finish, "Finish product",
                new WorkflowNodePositionDto(520m, 200m), null, null, null),
        },
        Edges: new[]
        {
            new WorkflowEdgeDto("start", "finish"),
        });
}
