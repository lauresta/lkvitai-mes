using FluentAssertions;
using LKvitai.MES.Modules.Shopfloor.Domain.Workflows;
using Xunit;

namespace LKvitai.MES.Tests.Shopfloor.Unit;

public class SmartWorkflowValidatorTests
{
    private static WorkflowGraphNode Start(string id = "start") =>
        new(id, WorkflowNodeKind.Start, "Start", null, null, null);

    private static WorkflowGraphNode Finish(string id = "finish") =>
        new(id, WorkflowNodeKind.Finish, "Finish", null, null, null);

    private static WorkflowGraphNode Task(string id, Guid station, int dur = 60, string? name = null) =>
        new(id, WorkflowNodeKind.Task, name ?? id, station, dur, null);

    private static WorkflowGraph Graph(WorkflowGraphNode[] nodes, (string From, string To)[] edges) =>
        new(nodes, edges.Select(e => new WorkflowGraphEdge(e.From, e.To)).ToList());

    private static bool Has(ValidationReport r, string ruleId) =>
        r.Findings.Any(f => f.RuleId == ruleId);

    [Fact]
    public void HealthyLinearGraph_IsPublishable_WithFullScore()
    {
        var station = Guid.NewGuid();
        var graph = Graph(
            new[] { Start(), Task("cut", station, 60), Finish() },
            new[] { ("start", "cut"), ("cut", "finish") });

        var report = SmartWorkflowValidator.Validate(graph);

        report.Publishable.Should().BeTrue();
        report.Summary.Errors.Should().Be(0);
        report.Score.Should().Be(100);
        report.Findings.Should().BeEmpty();
    }

    [Fact]
    public void Cycle_RaisesE5_AndBlocksPublish()
    {
        var s = Guid.NewGuid();
        var graph = Graph(
            new[] { Start(), Task("a", s), Task("b", s), Finish() },
            new[] { ("start", "a"), ("a", "b"), ("b", "a"), ("b", "finish") });

        var report = SmartWorkflowValidator.Validate(graph);

        Has(report, "E5").Should().BeTrue();
        report.Publishable.Should().BeFalse();
    }

    [Fact]
    public void DeadEndTask_RaisesE4()
    {
        var s = Guid.NewGuid();
        var graph = Graph(
            new[] { Start(), Task("a", s), Task("hang", s), Finish() },
            new[] { ("start", "a"), ("a", "finish"), ("a", "hang") });

        var report = SmartWorkflowValidator.Validate(graph);

        report.Findings.Should().Contain(f => f.RuleId == "E4" && f.Targets.NodeIds.Contains("hang"));
        report.Publishable.Should().BeFalse();
    }

    [Fact]
    public void UnreachableTask_RaisesE3()
    {
        var s = Guid.NewGuid();
        var graph = Graph(
            new[] { Start(), Task("a", s), Task("island", s), Finish() },
            new[] { ("start", "a"), ("a", "finish") });

        var report = SmartWorkflowValidator.Validate(graph);

        report.Findings.Should().Contain(f => f.RuleId == "E3" && f.Targets.NodeIds.Contains("island"));
    }

    [Fact]
    public void IncompleteTask_RaisesE7()
    {
        var graph = Graph(
            new[]
            {
                Start(),
                new WorkflowGraphNode("cut", WorkflowNodeKind.Task, "cut", null, null, null),
                Finish(),
            },
            new[] { ("start", "cut"), ("cut", "finish") });

        var report = SmartWorkflowValidator.Validate(graph);

        report.Findings.Count(f => f.RuleId == "E7").Should().Be(2); // no station + no duration
    }

    [Fact]
    public void MultipleStarts_RaisesE1()
    {
        var s = Guid.NewGuid();
        var graph = Graph(
            new[] { Start("s1"), Start("s2"), Task("cut", s), Finish() },
            new[] { ("s1", "cut"), ("cut", "finish") });

        SmartWorkflowValidator.Validate(graph).Findings.Should().Contain(f => f.RuleId == "E1");
    }

    [Fact]
    public void DuplicateEdge_RaisesE6()
    {
        var graph = Graph(
            new[] { Start(), Finish() },
            new[] { ("start", "finish"), ("start", "finish") });

        SmartWorkflowValidator.Validate(graph).Findings.Should().Contain(f => f.RuleId == "E6");
    }

    [Fact]
    public void BranchImbalance_RaisesW1_AndReportsMergeWait()
    {
        var l1 = Guid.NewGuid();
        var l2 = Guid.NewGuid();
        var l3 = Guid.NewGuid();
        var graph = Graph(
            new[] { Start(), Task("slow", l1, 10800), Task("fast", l2, 120), Task("merge", l3, 60), Finish() },
            new[] { ("start", "slow"), ("start", "fast"), ("slow", "merge"), ("fast", "merge"), ("merge", "finish") });

        var report = SmartWorkflowValidator.Validate(graph);

        Has(report, "W1").Should().BeTrue();
        report.Metrics.Merges.Should().Contain(m => m.NodeId == "merge" && m.WaitSec == 10680);
    }

    [Fact]
    public void CriticalPathAndBottleneck_AreComputed()
    {
        var l1 = Guid.NewGuid();
        var l2 = Guid.NewGuid();
        var graph = Graph(
            new[] { Start(), Task("slow", l1, 10800), Task("fast", l2, 120), Task("merge", l2, 60), Finish() },
            new[] { ("start", "slow"), ("start", "fast"), ("slow", "merge"), ("fast", "merge"), ("merge", "finish") });

        var report = SmartWorkflowValidator.Validate(graph);

        report.Metrics.CriticalPath!.NodeIds.Should().ContainInOrder("start", "slow", "merge", "finish");
        report.Metrics.LeadTimeSec.Should().Be(10860);
        report.Metrics.Bottleneck!.StationId.Should().Be(l1.ToString());
        report.Metrics.LineLoads.Should().HaveCount(2);
    }

    [Fact]
    public void WipRisk_RaisesW5()
    {
        var station = Guid.NewGuid();
        var stations = new[] { new WorkflowStationInfo(station, "FAB-CUT", "Fabric Cutting", 1, true) };
        var graph = Graph(
            new[] { Start(), Task("a", station), Task("b", station), Finish() },
            new[] { ("start", "a"), ("a", "b"), ("b", "finish") });

        var report = SmartWorkflowValidator.Validate(graph, stations);

        report.Findings.Should().Contain(f => f.RuleId == "W5" && f.Targets.StationIds.Contains(station.ToString()));
    }

    [Fact]
    public void FalseParallelism_RaisesW4()
    {
        var shared = Guid.NewGuid();
        var graph = Graph(
            new[] { Start(), Task("a", shared), Task("b", shared), Task("join", Guid.NewGuid()), Finish() },
            new[] { ("start", "a"), ("start", "b"), ("a", "join"), ("b", "join"), ("join", "finish") });

        SmartWorkflowValidator.Validate(graph).Findings.Should().Contain(f => f.RuleId == "W4");
    }

    [Fact]
    public void RedundantDependency_RaisesW9()
    {
        var s = Guid.NewGuid();
        var graph = Graph(
            new[] { Start(), Task("a", s), Task("b", s), Finish() },
            new[] { ("start", "a"), ("a", "b"), ("b", "finish"), ("start", "b") });

        SmartWorkflowValidator.Validate(graph).Findings
            .Should().Contain(f => f.RuleId == "W9" && f.Targets.EdgeIds.Contains("start->b"));
    }

    [Fact]
    public void PlaceholderName_RaisesH1()
    {
        var s = Guid.NewGuid();
        var graph = Graph(
            new[] { Start(), Task("a", s, 60, "NEW_TASK"), Finish() },
            new[] { ("start", "a"), ("a", "finish") });

        SmartWorkflowValidator.Validate(graph).Findings.Should().Contain(f => f.RuleId == "H1");
    }

    [Fact]
    public void InactiveStation_RaisesH3()
    {
        var station = Guid.NewGuid();
        var stations = new[] { new WorkflowStationInfo(station, "OLD", "Retired line", null, false) };
        var graph = Graph(
            new[] { Start(), Task("a", station), Finish() },
            new[] { ("start", "a"), ("a", "finish") });

        SmartWorkflowValidator.Validate(graph, stations).Findings
            .Should().Contain(f => f.RuleId == "H3" && f.Targets.NodeIds.Contains("a"));
    }

    [Fact]
    public void Score_DropsWithSeverity()
    {
        var s = Guid.NewGuid();
        var graph = Graph(
            new[] { Start(), Task("a", s), Task("hang", s), Finish() },
            new[] { ("start", "a"), ("a", "finish"), ("a", "hang") });

        var report = SmartWorkflowValidator.Validate(graph);

        report.Score.Should().BeLessThan(100);
        report.Publishable.Should().BeFalse();
    }
}
