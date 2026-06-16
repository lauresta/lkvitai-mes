using FluentAssertions;
using LKvitai.MES.Modules.Shopfloor.Domain.Workflows;
using Xunit;

namespace LKvitai.MES.Tests.Shopfloor.Unit;

public class WorkflowGraphValidatorTests
{
    private static WorkflowGraphNode Start(string id = "start") =>
        new(id, WorkflowNodeKind.Start, "Start", null, null, null);

    private static WorkflowGraphNode Finish(string id = "finish") =>
        new(id, WorkflowNodeKind.Finish, "Finish", null, null, null);

    private static WorkflowGraphNode Task(
        string id,
        Guid? stationId = null,
        int? durationSec = 60,
        string? taskType = null)
        => new(id, WorkflowNodeKind.Task, id, stationId ?? Guid.NewGuid(), durationSec, taskType);

    [Fact]
    public void Lenient_MinimalStartFinish_IsValid()
    {
        var graph = new WorkflowGraph(
            new[] { Start(), Finish() },
            new[] { new WorkflowGraphEdge("start", "finish") });

        WorkflowGraphValidator.ValidateLenient(graph).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Lenient_DetectsCycle()
    {
        var s = Guid.NewGuid();
        var graph = new WorkflowGraph(
            new[] { Start(), Task("a", s), Task("b", s), Finish() },
            new[]
            {
                new WorkflowGraphEdge("start", "a"),
                new WorkflowGraphEdge("a", "b"),
                new WorkflowGraphEdge("b", "a"),
            });

        var result = WorkflowGraphValidator.ValidateLenient(graph);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("cycle"));
    }

    [Fact]
    public void Lenient_DetectsDuplicateEdge()
    {
        var graph = new WorkflowGraph(
            new[] { Start(), Finish() },
            new[]
            {
                new WorkflowGraphEdge("start", "finish"),
                new WorkflowGraphEdge("start", "finish"),
            });

        var result = WorkflowGraphValidator.ValidateLenient(graph);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Duplicate edge"));
    }

    [Fact]
    public void Lenient_DetectsMissingEndpoint()
    {
        var graph = new WorkflowGraph(
            new[] { Start(), Finish() },
            new[] { new WorkflowGraphEdge("start", "ghost") });

        var result = WorkflowGraphValidator.ValidateLenient(graph);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("missing node endpoint"));
    }

    [Fact]
    public void Publish_FullyConnectedGraph_IsValid()
    {
        var station = Guid.NewGuid();
        var graph = new WorkflowGraph(
            new[] { Start(), Task("cut", station), Finish() },
            new[]
            {
                new WorkflowGraphEdge("start", "cut"),
                new WorkflowGraphEdge("cut", "finish"),
            });

        WorkflowGraphValidator.ValidateForPublish(graph).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Publish_TaskWithoutStationOrDuration_IsInvalid()
    {
        var graph = new WorkflowGraph(
            new[]
            {
                Start(),
                new WorkflowGraphNode("cut", WorkflowNodeKind.Task, "cut", null, null, null),
                Finish(),
            },
            new[]
            {
                new WorkflowGraphEdge("start", "cut"),
                new WorkflowGraphEdge("cut", "finish"),
            });

        var result = WorkflowGraphValidator.ValidateForPublish(graph);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("work station"));
        result.Errors.Should().Contain(e => e.Contains("positive duration"));
    }

    [Fact]
    public void Publish_UnreachableTask_IsInvalid()
    {
        var station = Guid.NewGuid();
        var graph = new WorkflowGraph(
            new[] { Start(), Task("connected", station), Task("island", station), Finish() },
            new[]
            {
                new WorkflowGraphEdge("start", "connected"),
                new WorkflowGraphEdge("connected", "finish"),
            });

        var result = WorkflowGraphValidator.ValidateForPublish(graph);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("island") && e.Contains("not reachable"));
    }

    [Fact]
    public void Publish_MultipleStarts_IsInvalid()
    {
        var station = Guid.NewGuid();
        var graph = new WorkflowGraph(
            new[] { Start("s1"), Start("s2"), Task("cut", station), Finish() },
            new[]
            {
                new WorkflowGraphEdge("s1", "cut"),
                new WorkflowGraphEdge("cut", "finish"),
            });

        var result = WorkflowGraphValidator.ValidateForPublish(graph);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("exactly one start"));
    }

    [Fact]
    public void Publish_FinishUnreachable_IsInvalid()
    {
        var station = Guid.NewGuid();
        var graph = new WorkflowGraph(
            new[] { Start(), Task("cut", station), Finish() },
            new[] { new WorkflowGraphEdge("start", "cut") });

        var result = WorkflowGraphValidator.ValidateForPublish(graph);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Finish node must be reachable"));
    }
}
