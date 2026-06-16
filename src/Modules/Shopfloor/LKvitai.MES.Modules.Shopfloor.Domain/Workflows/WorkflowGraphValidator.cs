namespace LKvitai.MES.Modules.Shopfloor.Domain.Workflows;

/// <summary>
/// Validates a <see cref="WorkflowGraph"/> in two tiers:
/// <list type="bullet">
///   <item><b>Lenient</b> — used when saving a work-in-progress draft.</item>
///   <item><b>Full</b> — used for publish-readiness and the editor Preview.</item>
/// </list>
/// </summary>
public static class WorkflowGraphValidator
{
    /// <summary>
    /// Lenient checks (save): edge endpoints exist, no duplicate edges, no cycles.
    /// </summary>
    public static GraphValidationResult ValidateLenient(WorkflowGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var errors = new List<string>();
        CheckStructure(graph, errors);
        return errors.Count == 0 ? GraphValidationResult.Valid : GraphValidationResult.Invalid(errors);
    }

    /// <summary>
    /// Full checks (publish / preview): everything in the lenient pass plus
    /// single start/finish, task completeness, reachability and orphan rules.
    /// </summary>
    public static GraphValidationResult ValidateForPublish(WorkflowGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var errors = new List<string>();
        CheckStructure(graph, errors);

        var starts = graph.Nodes.Where(n => n.Kind == WorkflowNodeKind.Start).ToList();
        var finishes = graph.Nodes.Where(n => n.Kind == WorkflowNodeKind.Finish).ToList();
        var tasks = graph.Nodes.Where(n => n.Kind == WorkflowNodeKind.Task).ToList();

        if (starts.Count != 1)
        {
            errors.Add($"Graph must have exactly one start node (found {starts.Count}).");
        }

        if (finishes.Count != 1)
        {
            errors.Add($"Graph must have exactly one finish node (found {finishes.Count}).");
        }

        foreach (var task in tasks)
        {
            if (task.WorkStationId is null || task.WorkStationId == Guid.Empty)
            {
                errors.Add($"Task '{task.Id}' must have a work station.");
            }

            if (task.DurationSec is null || task.DurationSec <= 0)
            {
                errors.Add($"Task '{task.Id}' must have a positive duration.");
            }
        }

        // Reachability only makes sense with a single, well-defined start/finish.
        if (starts.Count == 1 && finishes.Count == 1 && errors.All(e => !e.Contains("endpoint")))
        {
            var start = starts[0];
            var finish = finishes[0];

            var forward = ReachableFrom(graph, start.Id);
            var backward = CanReach(graph, finish.Id);

            if (!forward.Contains(finish.Id))
            {
                errors.Add("Finish node must be reachable from start.");
            }

            foreach (var task in tasks)
            {
                if (!forward.Contains(task.Id))
                {
                    errors.Add($"Task '{task.Id}' is not reachable from start.");
                }
                else if (!backward.Contains(task.Id))
                {
                    errors.Add($"Task '{task.Id}' is an orphan: it has no path forward to finish.");
                }
            }
        }

        return errors.Count == 0 ? GraphValidationResult.Valid : GraphValidationResult.Invalid(errors);
    }

    private static void CheckStructure(WorkflowGraph graph, List<string> errors)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in graph.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                errors.Add("Every node must have an id.");
                continue;
            }

            if (!ids.Add(node.Id))
            {
                errors.Add($"Duplicate node id '{node.Id}'.");
            }
        }

        var seenEdges = new HashSet<string>(StringComparer.Ordinal);
        foreach (var edge in graph.Edges)
        {
            if (!ids.Contains(edge.From) || !ids.Contains(edge.To))
            {
                errors.Add($"Edge '{edge.From}' → '{edge.To}' references a missing node endpoint.");
                continue;
            }

            if (!seenEdges.Add($"{edge.From}\u0001{edge.To}"))
            {
                errors.Add($"Duplicate edge '{edge.From}' → '{edge.To}'.");
            }
        }

        if (HasCycle(graph))
        {
            errors.Add("Graph must not contain a cycle.");
        }
    }

    private static bool HasCycle(WorkflowGraph graph)
    {
        var adjacency = BuildAdjacency(graph);
        var visiting = new HashSet<string>(StringComparer.Ordinal);
        var done = new HashSet<string>(StringComparer.Ordinal);

        foreach (var node in graph.Nodes)
        {
            if (!done.Contains(node.Id) && VisitForCycle(node.Id, adjacency, visiting, done))
            {
                return true;
            }
        }

        return false;
    }

    private static bool VisitForCycle(
        string node,
        Dictionary<string, List<string>> adjacency,
        HashSet<string> visiting,
        HashSet<string> done)
    {
        visiting.Add(node);
        if (adjacency.TryGetValue(node, out var next))
        {
            foreach (var target in next)
            {
                if (visiting.Contains(target))
                {
                    return true;
                }

                if (!done.Contains(target) && VisitForCycle(target, adjacency, visiting, done))
                {
                    return true;
                }
            }
        }

        visiting.Remove(node);
        done.Add(node);
        return false;
    }

    private static HashSet<string> ReachableFrom(WorkflowGraph graph, string start)
    {
        var adjacency = BuildAdjacency(graph);
        return Traverse(start, adjacency);
    }

    private static HashSet<string> CanReach(WorkflowGraph graph, string target)
    {
        var reverse = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var edge in graph.Edges)
        {
            if (!reverse.TryGetValue(edge.To, out var list))
            {
                list = new List<string>();
                reverse[edge.To] = list;
            }

            list.Add(edge.From);
        }

        return Traverse(target, reverse);
    }

    private static HashSet<string> Traverse(string from, IReadOnlyDictionary<string, List<string>> adjacency)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        stack.Push(from);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!seen.Add(current))
            {
                continue;
            }

            if (adjacency.TryGetValue(current, out var next))
            {
                foreach (var target in next)
                {
                    stack.Push(target);
                }
            }
        }

        return seen;
    }

    private static Dictionary<string, List<string>> BuildAdjacency(WorkflowGraph graph)
    {
        var adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var edge in graph.Edges)
        {
            if (!adjacency.TryGetValue(edge.From, out var list))
            {
                list = new List<string>();
                adjacency[edge.From] = list;
            }

            list.Add(edge.To);
        }

        return adjacency;
    }
}
