namespace LKvitai.MES.Modules.Shopfloor.Domain.Workflows;

/// <summary>
/// The smart workflow validator (shopfloor-12). Runs the full rule catalog —
/// errors (E1–E7), warnings (W1, W3–W9, W11) and hints (H1–H4) — and computes
/// the production metrics (critical path, bottleneck, throughput, per-line load,
/// branch imbalance) into a single <see cref="ValidationReport"/>.
///
/// <para>Framework-free and deterministic: the same engine runs in the editor
/// (Validate / Preview) and on the server (Publish gate).</para>
/// </summary>
public static class SmartWorkflowValidator
{
    // Tunable thresholds (configuration candidates — S-7). Kept as constants for now.
    private const int BranchImbalanceWaitSec = 30 * 60;   // W1: flag a merge wait above 30 min
    private const double DominantTaskShare = 0.60;        // W7: one task > 60% of lead time
    private const double LineImbalanceShare = 0.60;       // W8: one line > 60% of total work
    private const int LongChainLength = 4;                // W11: >= 4 consecutive same-line tasks
    private const double DurationOutlierFactor = 8.0;     // H2: > 8x the median sibling duration
    private const int AbsurdDurationSec = 24 * 60 * 60;   // H2: > 24h is almost certainly wrong

    private const int ErrorWeight = 18;
    private const int WarningWeight = 6;
    private const int HintWeight = 2;

    public static ValidationReport Validate(
        WorkflowGraph graph,
        IReadOnlyCollection<WorkflowStationInfo>? stations = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var ctx = new Context(graph, stations ?? Array.Empty<WorkflowStationInfo>());
        var findings = new List<ValidationFinding>();

        CheckErrors(ctx, findings);
        var metrics = ComputeMetrics(ctx);
        CheckWarnings(ctx, metrics, findings);
        CheckHints(ctx, findings);

        var summary = new ValidationSummary(
            findings.Count(f => f.Severity == ValidationSeverity.Error),
            findings.Count(f => f.Severity == ValidationSeverity.Warning),
            findings.Count(f => f.Severity == ValidationSeverity.Hint));

        var score = Math.Clamp(
            100 - (summary.Errors * ErrorWeight) - (summary.Warnings * WarningWeight) - (summary.Hints * HintWeight),
            0, 100);

        return new ValidationReport(score, summary.Errors == 0, summary, metrics, findings);
    }

    // ── Errors ─────────────────────────────────────────────────────────────
    private static void CheckErrors(Context ctx, List<ValidationFinding> findings)
    {
        // E6 — broken edges: missing endpoint, self-loop, duplicate.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var e in ctx.Graph.Edges)
        {
            if (!ctx.NodeIds.Contains(e.From) || !ctx.NodeIds.Contains(e.To))
            {
                findings.Add(Error("E6", "Broken edge",
                    $"Edge {e.From} → {e.To} references a missing node.",
                    new ValidationTargets(Array.Empty<string>(), new[] { ValidationTargets.EdgeId(e.From, e.To) }, Array.Empty<string>())));
                continue;
            }

            if (string.Equals(e.From, e.To, StringComparison.Ordinal))
            {
                findings.Add(Error("E6", "Broken edge", $"Self-loop on node {e.From}.",
                    EdgeTarget(e.From, e.To)));
            }

            if (!seen.Add($"{e.From}\u0001{e.To}"))
            {
                findings.Add(Error("E6", "Broken edge", $"Duplicate edge {e.From} → {e.To}.",
                    EdgeTarget(e.From, e.To)));
            }
        }

        // E1 / E2 — single start / single finish.
        if (ctx.Starts.Count != 1)
        {
            findings.Add(Error("E1", "Single start",
                $"Graph must have exactly one start node (found {ctx.Starts.Count}).",
                new ValidationTargets(ctx.Starts.Select(n => n.Id).ToList(), Array.Empty<string>(), Array.Empty<string>())));
        }

        if (ctx.Finishes.Count != 1)
        {
            findings.Add(Error("E2", "Single finish",
                $"Graph must have exactly one finish node (found {ctx.Finishes.Count}). Parallel branches must converge to one finish.",
                new ValidationTargets(ctx.Finishes.Select(n => n.Id).ToList(), Array.Empty<string>(), Array.Empty<string>())));
        }

        // E7 — incomplete task: missing line or non-positive duration.
        foreach (var t in ctx.Tasks)
        {
            if (t.WorkStationId is null || t.WorkStationId == Guid.Empty)
            {
                findings.Add(Error("E7", "Incomplete task",
                    $"Task '{NameOf(t)}' has no work station.", ValidationTargets.Nodes(t.Id)));
            }

            if (t.DurationSec is null || t.DurationSec <= 0)
            {
                findings.Add(Error("E7", "Incomplete task",
                    $"Task '{NameOf(t)}' has no positive duration.", ValidationTargets.Nodes(t.Id)));
            }
        }

        // E5 — cycle (nodes that fall outside any topological order).
        if (ctx.CyclicNodes.Count > 0)
        {
            var cyclicEdges = ctx.Graph.Edges
                .Where(e => ctx.CyclicNodes.Contains(e.From) && ctx.CyclicNodes.Contains(e.To))
                .Select(e => ValidationTargets.EdgeId(e.From, e.To))
                .ToList();
            findings.Add(Error("E5", "Cycle",
                "The flow contains a cycle, so it can never complete.",
                new ValidationTargets(ctx.CyclicNodes.ToList(), cyclicEdges, Array.Empty<string>())));
        }

        // E3 / E4 — reachability (only meaningful with a single start + finish, acyclic).
        if (ctx.Starts.Count == 1 && ctx.Finishes.Count == 1 && ctx.CyclicNodes.Count == 0)
        {
            if (!ctx.ReachableFromStart.Contains(ctx.Finishes[0].Id))
            {
                findings.Add(Error("E4", "Dead-end flow",
                    "Finish is not reachable from start.", ValidationTargets.Nodes(ctx.Finishes[0].Id)));
            }

            foreach (var t in ctx.Tasks)
            {
                if (!ctx.ReachableFromStart.Contains(t.Id))
                {
                    findings.Add(Error("E3", "Unreachable task",
                        $"Task '{NameOf(t)}' has no path from start — it never begins.",
                        ValidationTargets.Nodes(t.Id)));
                }
                else if (!ctx.CanReachFinish.Contains(t.Id))
                {
                    findings.Add(Error("E4", "Dead-end task",
                        $"Task '{NameOf(t)}' hangs — no path to finish.", ValidationTargets.Nodes(t.Id)));
                }
            }
        }
    }

    // ── Metrics ────────────────────────────────────────────────────────────
    private static ValidationMetrics ComputeMetrics(Context ctx)
    {
        // Per-line load works regardless of DAG validity.
        var lineLoads = ctx.Tasks
            .Where(t => t.WorkStationId is { } s && s != Guid.Empty)
            .GroupBy(t => t.WorkStationId!.Value)
            .Select(g => new LineLoadInfo(
                g.Key.ToString(),
                ctx.StationName(g.Key),
                g.Sum(t => t.DurationSec ?? 0),
                g.Count()))
            .OrderByDescending(l => l.LoadSec)
            .ToList();

        BottleneckInfo? bottleneck = null;
        var throughput = 0;
        if (lineLoads.Count > 0 && lineLoads[0].LoadSec > 0)
        {
            var top = lineLoads[0];
            bottleneck = new BottleneckInfo(top.StationId, top.StationName, top.LoadSec);
            throughput = (int)Math.Round(3600.0 / top.LoadSec, MidpointRounding.AwayFromZero);
        }

        // Critical path + merges require a single start/finish and an acyclic graph.
        CriticalPathInfo? criticalPath = null;
        var merges = new List<MergeInfo>();
        var leadTime = 0;
        if (ctx.Starts.Count == 1 && ctx.Finishes.Count == 1 && ctx.CyclicNodes.Count == 0)
        {
            var (path, total) = ctx.LongestPathToFinish();
            if (path.Count > 0)
            {
                criticalPath = new CriticalPathInfo(path, total);
                leadTime = total;
            }

            foreach (var node in ctx.Graph.Nodes)
            {
                var arrivals = ctx.Predecessors(node.Id)
                    .Where(p => ctx.ReachableFromStart.Contains(p))
                    .Select(ctx.DistanceTo)
                    .Where(d => d >= 0)
                    .ToList();
                if (arrivals.Count < 2)
                {
                    continue;
                }

                var max = arrivals.Max();
                var min = arrivals.Min();
                merges.Add(new MergeInfo(node.Id, max, min, max - min));
            }
        }

        return new ValidationMetrics(leadTime, criticalPath, bottleneck, throughput, lineLoads, merges);
    }

    // ── Warnings ───────────────────────────────────────────────────────────
    private static void CheckWarnings(Context ctx, ValidationMetrics metrics, List<ValidationFinding> findings)
    {
        // W1 — branch imbalance / starvation at a merge.
        foreach (var m in metrics.Merges.Where(m => m.WaitSec >= BranchImbalanceWaitSec))
        {
            findings.Add(new ValidationFinding("W1", ValidationSeverity.Warning, "Branch imbalance",
                $"Branches into '{ctx.NodeName(m.NodeId)}' differ by {Hms(m.WaitSec)} — the fast branch idles ({Hms(m.MaxInSec)} vs {Hms(m.MinInSec)}).",
                ValidationTargets.Nodes(m.NodeId),
                new Dictionary<string, double> { ["waitSec"] = m.WaitSec, ["maxInSec"] = m.MaxInSec, ["minInSec"] = m.MinInSec }));
        }

        // W3 — bottleneck line (only meaningful when a real constraint exists).
        if (metrics.Bottleneck is { } b && metrics.LineLoads.Count >= 2)
        {
            var nodes = ctx.TaskNodesOnStation(b.StationId);
            findings.Add(new ValidationFinding("W3", ValidationSeverity.Warning, "Bottleneck line",
                $"'{b.StationName}' is the bottleneck — caps throughput at ~{metrics.ThroughputPerHour}/hr.",
                new ValidationTargets(nodes, Array.Empty<string>(), new[] { b.StationId }),
                new Dictionary<string, double> { ["loadSec"] = b.LoadSec, ["throughputPerHour"] = metrics.ThroughputPerHour }));
        }

        // W4 — false parallelism: concurrent tasks pinned to the same line.
        if (ctx.CyclicNodes.Count == 0)
        {
            foreach (var group in ctx.TasksByStation())
            {
                var concurrent = ctx.ConcurrentTaskSet(group.Value);
                if (concurrent.Count >= 2)
                {
                    findings.Add(new ValidationFinding("W4", ValidationSeverity.Warning, "False parallelism",
                        $"{concurrent.Count} tasks look parallel but share line '{ctx.StationName(group.Key)}' — they queue (real time = sum, not max).",
                        new ValidationTargets(concurrent, Array.Empty<string>(), new[] { group.Key.ToString() })));
                }
            }
        }

        // W5 — WIP / CONWIP risk.
        foreach (var group in ctx.TasksByStation())
        {
            var station = ctx.Station(group.Key);
            if (station?.WipLimit is { } limit && limit > 0 && group.Value.Count > limit)
            {
                findings.Add(new ValidationFinding("W5", ValidationSeverity.Warning, "WIP risk",
                    $"{group.Value.Count} tasks routed through '{station.Name}' (WIP limit {limit}) — a guaranteed queue.",
                    new ValidationTargets(group.Value.Select(t => t.Id).ToList(), Array.Empty<string>(), new[] { group.Key.ToString() }),
                    new Dictionary<string, double> { ["routed"] = group.Value.Count, ["wipLimit"] = limit }));
            }
        }

        // W6 — line ping-pong: A → B → A (same line bracketing a different one).
        var pingPong = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in ctx.Tasks)
        {
            if (t.WorkStationId is not { } a || a == Guid.Empty)
            {
                continue;
            }

            foreach (var midId in ctx.Successors(t.Id))
            {
                if (ctx.StationOf(midId) is not { } mid || mid == a)
                {
                    continue;
                }

                foreach (var endId in ctx.Successors(midId))
                {
                    if (ctx.StationOf(endId) == a)
                    {
                        var key = string.CompareOrdinal(a.ToString(), mid.ToString()) <= 0
                            ? $"{a}|{mid}" : $"{mid}|{a}";
                        if (pingPong.Add(key))
                        {
                            findings.Add(new ValidationFinding("W6", ValidationSeverity.Warning, "Line ping-pong",
                                $"Flow bounces '{ctx.StationName(a)}' → '{ctx.StationName(mid)}' → '{ctx.StationName(a)}' — extra transport/changeovers. Group consecutive same-line tasks.",
                                ValidationTargets.Nodes(t.Id, midId, endId)));
                        }
                    }
                }
            }
        }

        // W7 — dominant task: one operation owns most of the lead time
        // (only meaningful when there is more than one task to compare against).
        if (metrics.CriticalPath is { } cp && cp.DurationSec > 0 && ctx.Tasks.Count >= 2)
        {
            foreach (var t in ctx.Tasks)
            {
                var dur = t.DurationSec ?? 0;
                var share = (double)dur / cp.DurationSec;
                if (share >= DominantTaskShare)
                {
                    findings.Add(new ValidationFinding("W7", ValidationSeverity.Warning, "Dominant task",
                        $"'{NameOf(t)}' is {Pct(share)} of the lead time — the whole flow hinges on one operation.",
                        ValidationTargets.Nodes(t.Id),
                        new Dictionary<string, double> { ["percent"] = Math.Round(share * 100, 1) }));
                }
            }
        }

        // W8 — line load imbalance: one line carries most of the work.
        var totalLoad = metrics.LineLoads.Sum(l => (long)l.LoadSec);
        if (totalLoad > 0 && metrics.LineLoads.Count >= 2)
        {
            var top = metrics.LineLoads[0];
            var share = (double)top.LoadSec / totalLoad;
            if (share >= LineImbalanceShare)
            {
                findings.Add(new ValidationFinding("W8", ValidationSeverity.Warning, "Line load imbalance",
                    $"'{top.StationName}' carries {Pct(share)} of the total work while other lines idle — rebalance opportunity.",
                    new ValidationTargets(ctx.TaskNodesOnStation(top.StationId), Array.Empty<string>(), new[] { top.StationId }),
                    new Dictionary<string, double> { ["percent"] = Math.Round(share * 100, 1) }));
            }
        }

        // W9 — redundant dependency: A → C while A ⇒ … ⇒ C already exists.
        foreach (var e in ctx.Graph.Edges)
        {
            if (!ctx.NodeIds.Contains(e.From) || !ctx.NodeIds.Contains(e.To) || string.Equals(e.From, e.To, StringComparison.Ordinal))
            {
                continue;
            }

            if (ctx.HasAlternatePath(e.From, e.To))
            {
                findings.Add(new ValidationFinding("W9", ValidationSeverity.Warning, "Redundant dependency",
                    $"Edge '{ctx.NodeName(e.From)}' → '{ctx.NodeName(e.To)}' is redundant — already reached another way. Over-constrains scheduling.",
                    EdgeTarget(e.From, e.To)));
            }
        }

        // W11 — long single-line chain.
        var chain = ctx.LongestSameLineChain();
        if (chain.Count >= LongChainLength)
        {
            findings.Add(new ValidationFinding("W11", ValidationSeverity.Warning, "Long single-line chain",
                $"{chain.Count} sequential tasks on '{ctx.StationName(ctx.StationOf(chain[0])!.Value)}' — a serial bottleneck; candidate to parallelize.",
                new ValidationTargets(chain, Array.Empty<string>(), Array.Empty<string>())));
        }
    }

    // ── Hints ──────────────────────────────────────────────────────────────
    private static void CheckHints(Context ctx, List<ValidationFinding> findings)
    {
        // H1 — name hygiene: empty / placeholder / duplicate task names.
        var placeholders = new HashSet<string>(
            new[] { "task", "new task", "new_task", "newtask", "untitled" }, StringComparer.OrdinalIgnoreCase);
        foreach (var t in ctx.Tasks)
        {
            var name = t.Name?.Trim() ?? string.Empty;
            if (name.Length == 0 || placeholders.Contains(name))
            {
                findings.Add(new ValidationFinding("H1", ValidationSeverity.Hint, "Name hygiene",
                    name.Length == 0 ? "A task has no name — give it a real name." : $"Task named '{name}' — give it a real name.",
                    ValidationTargets.Nodes(t.Id)));
            }
        }

        foreach (var dup in ctx.Tasks
                     .Where(t => !string.IsNullOrWhiteSpace(t.Name))
                     .GroupBy(t => t.Name!.Trim(), StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Count() > 1))
        {
            findings.Add(new ValidationFinding("H1", ValidationSeverity.Hint, "Duplicate name",
                $"{dup.Count()} tasks share the name '{dup.Key}' — make them distinct.",
                new ValidationTargets(dup.Select(t => t.Id).ToList(), Array.Empty<string>(), Array.Empty<string>())));
        }

        // H2 — duration outlier vs sibling tasks.
        var durations = ctx.Tasks.Where(t => t.DurationSec is > 0).Select(t => t.DurationSec!.Value).ToList();
        if (durations.Count >= 3)
        {
            var median = Median(durations);
            foreach (var t in ctx.Tasks.Where(t => t.DurationSec is > 0))
            {
                var dur = t.DurationSec!.Value;
                if ((median > 0 && dur > median * DurationOutlierFactor) || dur > AbsurdDurationSec)
                {
                    findings.Add(new ValidationFinding("H2", ValidationSeverity.Hint, "Duration outlier",
                        $"'{NameOf(t)}' is {Hms(dur)} — far from the typical {Hms(median)}. Double-check the duration.",
                        ValidationTargets.Nodes(t.Id),
                        new Dictionary<string, double> { ["durationSec"] = dur, ["medianSec"] = median }));
                }
            }
        }

        // H3 — bad station reference (unknown or inactive station).
        if (ctx.HasStationCatalog)
        {
            foreach (var t in ctx.Tasks)
            {
                if (t.WorkStationId is not { } sid || sid == Guid.Empty)
                {
                    continue; // covered by E7
                }

                var station = ctx.Station(sid);
                if (station is null)
                {
                    findings.Add(new ValidationFinding("H3", ValidationSeverity.Hint, "Bad station ref",
                        $"'{NameOf(t)}' points at a work station that no longer exists.",
                        new ValidationTargets(new[] { t.Id }, Array.Empty<string>(), new[] { sid.ToString() })));
                }
                else if (!station.IsActive)
                {
                    findings.Add(new ValidationFinding("H3", ValidationSeverity.Hint, "Inactive station",
                        $"'{NameOf(t)}' runs on '{station.Name}', which is inactive.",
                        new ValidationTargets(new[] { t.Id }, Array.Empty<string>(), new[] { sid.ToString() })));
                }
            }
        }

        // H4 — no convergence: parallel branches each go straight to finish.
        if (ctx.Starts.Count == 1 && ctx.Finishes.Count == 1 && ctx.CyclicNodes.Count == 0)
        {
            var finish = ctx.Finishes[0];
            var taskPreds = ctx.Predecessors(finish.Id).Where(p => ctx.IsTask(p)).ToList();
            if (taskPreds.Count >= 2)
            {
                findings.Add(new ValidationFinding("H4", ValidationSeverity.Hint, "No convergence",
                    $"{taskPreds.Count} branches reach finish with no shared assembly step — confirm that's intended.",
                    new ValidationTargets(taskPreds.Append(finish.Id).ToList(), Array.Empty<string>(), Array.Empty<string>())));
            }
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    private static ValidationFinding Error(string id, string title, string message, ValidationTargets targets) =>
        new(id, ValidationSeverity.Error, title, message, targets);

    private static ValidationTargets EdgeTarget(string from, string to) =>
        new(Array.Empty<string>(), new[] { ValidationTargets.EdgeId(from, to) }, Array.Empty<string>());

    private static string NameOf(WorkflowGraphNode n) =>
        string.IsNullOrWhiteSpace(n.Name) ? n.Id : n.Name.Trim();

    private static string Pct(double share) => $"{Math.Round(share * 100)}%";

    private static int Median(List<int> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }

    private static string Hms(int sec)
    {
        if (sec < 60)
        {
            return $"{sec}s";
        }

        var h = sec / 3600;
        var m = (sec % 3600 + 30) / 60;
        if (h > 0 && m > 0)
        {
            return $"{h}h {m}m";
        }

        return h > 0 ? $"{h}h" : $"{m}m";
    }

    /// <summary>Pre-computed graph helpers shared across rule passes.</summary>
    private sealed class Context
    {
        private const int NegativeInfinity = int.MinValue / 4;

        private readonly Dictionary<string, WorkflowGraphNode> _byId;
        private readonly Dictionary<string, List<string>> _adj;
        private readonly Dictionary<string, List<string>> _rev;
        private readonly Dictionary<Guid, WorkflowStationInfo> _stations;
        private readonly Dictionary<string, int> _distance = new(StringComparer.Ordinal);
        private List<string>? _topoOrder;

        public Context(WorkflowGraph graph, IReadOnlyCollection<WorkflowStationInfo> stations)
        {
            Graph = graph;
            _byId = graph.Nodes
                .Where(n => !string.IsNullOrWhiteSpace(n.Id))
                .GroupBy(n => n.Id, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            NodeIds = new HashSet<string>(_byId.Keys, StringComparer.Ordinal);

            _adj = NodeIds.ToDictionary(id => id, _ => new List<string>(), StringComparer.Ordinal);
            _rev = NodeIds.ToDictionary(id => id, _ => new List<string>(), StringComparer.Ordinal);
            foreach (var e in graph.Edges)
            {
                if (NodeIds.Contains(e.From) && NodeIds.Contains(e.To) && !string.Equals(e.From, e.To, StringComparison.Ordinal))
                {
                    _adj[e.From].Add(e.To);
                    _rev[e.To].Add(e.From);
                }
            }

            _stations = stations
                .GroupBy(s => s.Id)
                .ToDictionary(g => g.Key, g => g.First());
            HasStationCatalog = stations.Count > 0;

            Starts = graph.Nodes.Where(n => n.Kind == WorkflowNodeKind.Start).ToList();
            Finishes = graph.Nodes.Where(n => n.Kind == WorkflowNodeKind.Finish).ToList();
            Tasks = graph.Nodes.Where(n => n.Kind == WorkflowNodeKind.Task).ToList();

            TopoSort();
            ReachableFromStart = Starts.Count == 1 ? Traverse(Starts[0].Id, _adj) : new HashSet<string>(StringComparer.Ordinal);
            CanReachFinish = Finishes.Count == 1 ? Traverse(Finishes[0].Id, _rev) : new HashSet<string>(StringComparer.Ordinal);
        }

        public WorkflowGraph Graph { get; }

        public HashSet<string> NodeIds { get; }

        public IReadOnlyList<WorkflowGraphNode> Starts { get; }

        public IReadOnlyList<WorkflowGraphNode> Finishes { get; }

        public IReadOnlyList<WorkflowGraphNode> Tasks { get; }

        public HashSet<string> CyclicNodes { get; private set; } = new(StringComparer.Ordinal);

        public HashSet<string> ReachableFromStart { get; }

        public HashSet<string> CanReachFinish { get; }

        public bool HasStationCatalog { get; }

        public IReadOnlyList<string> Successors(string id) =>
            _adj.TryGetValue(id, out var s) ? s : Array.Empty<string>();

        public IReadOnlyList<string> Predecessors(string id) =>
            _rev.TryGetValue(id, out var p) ? p : Array.Empty<string>();

        public bool IsTask(string id) => _byId.TryGetValue(id, out var n) && n.Kind == WorkflowNodeKind.Task;

        public Guid? StationOf(string id) =>
            _byId.TryGetValue(id, out var n) && n.WorkStationId is { } s && s != Guid.Empty ? s : null;

        public WorkflowStationInfo? Station(Guid id) => _stations.TryGetValue(id, out var s) ? s : null;

        public string StationName(Guid id) => _stations.TryGetValue(id, out var s) ? s.Name : id.ToString();

        public string NodeName(string id) =>
            _byId.TryGetValue(id, out var n) && !string.IsNullOrWhiteSpace(n.Name) ? n.Name.Trim() : id;

        public IReadOnlyDictionary<Guid, List<WorkflowGraphNode>> TasksByStation() =>
            Tasks.Where(t => t.WorkStationId is { } s && s != Guid.Empty)
                .GroupBy(t => t.WorkStationId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

        public IReadOnlyList<string> TaskNodesOnStation(string stationId) =>
            Guid.TryParse(stationId, out var guid)
                ? Tasks.Where(t => t.WorkStationId == guid).Select(t => t.Id).ToList()
                : Array.Empty<string>();

        public int DistanceTo(string id) => _distance.TryGetValue(id, out var d) ? d : 0;

        /// <summary>Tasks within <paramref name="group"/> that are mutually unreachable (truly concurrent).</summary>
        public List<string> ConcurrentTaskSet(List<WorkflowGraphNode> group)
        {
            var concurrent = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < group.Count; i++)
            {
                for (var j = i + 1; j < group.Count; j++)
                {
                    var a = group[i].Id;
                    var b = group[j].Id;
                    if (!Reaches(a, b) && !Reaches(b, a))
                    {
                        concurrent.Add(a);
                        concurrent.Add(b);
                    }
                }
            }

            return concurrent.ToList();
        }

        /// <summary>True if <paramref name="to"/> is reachable from <paramref name="from"/> ignoring the direct edge.</summary>
        public bool HasAlternatePath(string from, string to)
        {
            var stack = new Stack<string>();
            foreach (var next in Successors(from))
            {
                if (!string.Equals(next, to, StringComparison.Ordinal))
                {
                    stack.Push(next);
                }
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (string.Equals(cur, to, StringComparison.Ordinal))
                {
                    return true;
                }

                if (!seen.Add(cur))
                {
                    continue;
                }

                foreach (var next in Successors(cur))
                {
                    stack.Push(next);
                }
            }

            return false;
        }

        public (IReadOnlyList<string> Path, int Total) LongestPathToFinish()
        {
            if (_topoOrder is null || Starts.Count != 1 || Finishes.Count != 1)
            {
                return (Array.Empty<string>(), 0);
            }

            var pred = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var id in _topoOrder)
            {
                pred[id] = null;
            }

            foreach (var id in _topoOrder)
            {
                foreach (var next in Successors(id))
                {
                    var candidate = _distance[id] + Weight(next);
                    if (candidate > _distance[next])
                    {
                        _distance[next] = candidate;
                        pred[next] = id;
                    }
                }
            }

            var finishId = Finishes[0].Id;
            if (!_distance.TryGetValue(finishId, out var finishDist) || finishDist < 0)
            {
                return (Array.Empty<string>(), 0);
            }

            var path = new List<string>();
            string? cursor = finishId;
            while (cursor is not null)
            {
                path.Add(cursor);
                cursor = pred[cursor];
            }

            path.Reverse();
            return (path, _distance[finishId]);
        }

        public List<string> LongestSameLineChain()
        {
            if (_topoOrder is null)
            {
                return new List<string>();
            }

            var best = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            List<string> longest = new();
            foreach (var id in _topoOrder)
            {
                if (!IsTask(id) || StationOf(id) is not { } station)
                {
                    continue;
                }

                var chain = new List<string> { id };
                foreach (var p in Predecessors(id))
                {
                    if (StationOf(p) == station && best.TryGetValue(p, out var prev) && prev.Count + 1 > chain.Count)
                    {
                        chain = new List<string>(prev) { id };
                    }
                }

                best[id] = chain;
                if (chain.Count > longest.Count)
                {
                    longest = chain;
                }
            }

            return longest;
        }

        private int Weight(string id) =>
            _byId.TryGetValue(id, out var n) && n.Kind == WorkflowNodeKind.Task ? Math.Max(0, n.DurationSec ?? 0) : 0;

        private bool Reaches(string from, string to) => Traverse(from, _adj).Contains(to);

        private void TopoSort()
        {
            var indeg = NodeIds.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
            foreach (var kv in _adj)
            {
                foreach (var to in kv.Value)
                {
                    indeg[to]++;
                }
            }

            var queue = new Queue<string>(indeg.Where(kv => kv.Value == 0).Select(kv => kv.Key));
            var order = new List<string>();
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                order.Add(id);
                foreach (var next in _adj[id])
                {
                    if (--indeg[next] == 0)
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            if (order.Count == NodeIds.Count)
            {
                _topoOrder = order;
                // Base distances: a source node starts at its own weight; every other
                // node starts at a sentinel so the first predecessor relaxation wins
                // (and records a predecessor for critical-path backtracking).
                foreach (var id in order)
                {
                    _distance[id] = _rev[id].Count == 0 ? Weight(id) : NegativeInfinity;
                }
            }
            else
            {
                CyclicNodes = new HashSet<string>(NodeIds.Except(order), StringComparer.Ordinal);
            }
        }

        private static HashSet<string> Traverse(string from, IReadOnlyDictionary<string, List<string>> adjacency)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var stack = new Stack<string>();
            stack.Push(from);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (!seen.Add(cur))
                {
                    continue;
                }

                if (adjacency.TryGetValue(cur, out var next))
                {
                    foreach (var n in next)
                    {
                        stack.Push(n);
                    }
                }
            }

            return seen;
        }
    }
}
