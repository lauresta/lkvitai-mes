using FluentAssertions;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.E2E;

public class OutboundWorkflowTests
{
    private static readonly string[] OutboundFlow = ["CreateOrder", "Allocate", "Pick", "Pack", "Dispatch"];

    public static IEnumerable<object[]> OutboundScenarios => WorkflowScenarioLoader.Load("outbound-scenarios.json");

    [Theory]
    [MemberData(nameof(OutboundScenarios))]
    public void CreateToDispatch_completes_expected_steps(WorkflowScenario scenario)
    {
        var result = WorkflowSimulator.Execute(scenario, OutboundFlow);
        result.ExecutedSteps.Should().ContainInOrder(OutboundFlow);
    }

    [Theory]
    [MemberData(nameof(OutboundScenarios))]
    public void CreateToDispatch_assigns_isolated_parallel_database(WorkflowScenario scenario)
    {
        var result = WorkflowSimulator.Execute(scenario, OutboundFlow);
        result.DatabaseName.Should().MatchRegex("^test-db-[1-4]$");
    }

    [Theory]
    [MemberData(nameof(OutboundScenarios))]
    public void CreateToDispatch_requires_positive_quantity(WorkflowScenario scenario)
    {
        var result = WorkflowSimulator.Execute(scenario, OutboundFlow);
        result.FinalQuantity.Should().BeGreaterThan(0);
    }

    [Theory]
    [MemberData(nameof(OutboundScenarios))]
    public void CreateToDispatch_includes_allocate_step(WorkflowScenario scenario)
    {
        var result = WorkflowSimulator.Execute(scenario, OutboundFlow);
        result.ExecutedSteps.Should().Contain("Allocate");
    }

    [Theory]
    [MemberData(nameof(OutboundScenarios))]
    public void CreateToDispatch_includes_pick_step(WorkflowScenario scenario)
    {
        var result = WorkflowSimulator.Execute(scenario, OutboundFlow);
        result.ExecutedSteps.Should().Contain("Pick");
    }

    [Theory]
    [MemberData(nameof(OutboundScenarios))]
    public void CreateToDispatch_includes_pack_step(WorkflowScenario scenario)
    {
        var result = WorkflowSimulator.Execute(scenario, OutboundFlow);
        result.ExecutedSteps.Should().Contain("Pack");
    }

    [Theory]
    [MemberData(nameof(OutboundScenarios))]
    public void CreateToDispatch_tracks_scenario_identity(WorkflowScenario scenario)
    {
        var result = WorkflowSimulator.Execute(scenario, OutboundFlow);
        result.ScenarioId.Should().Be(scenario.Id);
    }
}

