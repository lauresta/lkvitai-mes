using FluentAssertions;
using Xunit;

namespace LKvitai.MES.Tests.E2E;

public class InboundWorkflowTests
{
    private static readonly string[] InboundFlow = ["CreateShipment", "Receive", "QC", "Putaway"];

    public static IEnumerable<object[]> InboundScenarios => WorkflowScenarioLoader.Load("inbound-scenarios.json");

    [Theory]
    [MemberData(nameof(InboundScenarios))]
    public void ReceiveAndPutaway_completes_expected_steps(WorkflowScenario scenario)
    {
        var result = WorkflowSimulator.Execute(scenario, InboundFlow);
        result.ExecutedSteps.Should().ContainInOrder(InboundFlow);
    }

    [Theory]
    [MemberData(nameof(InboundScenarios))]
    public void ReceiveAndPutaway_keeps_positive_quantity(WorkflowScenario scenario)
    {
        var result = WorkflowSimulator.Execute(scenario, InboundFlow);
        result.FinalQuantity.Should().BeGreaterThan(0);
    }

    [Theory]
    [MemberData(nameof(InboundScenarios))]
    public void ReceiveAndPutaway_assigns_isolated_parallel_database(WorkflowScenario scenario)
    {
        var result = WorkflowSimulator.Execute(scenario, InboundFlow);
        result.DatabaseName.Should().MatchRegex("^test-db-[1-4]$");
    }

    [Theory]
    [MemberData(nameof(InboundScenarios))]
    public void ReceiveAndPutaway_includes_qc_step(WorkflowScenario scenario)
    {
        var result = WorkflowSimulator.Execute(scenario, InboundFlow);
        result.ExecutedSteps.Should().Contain("QC");
    }

    [Theory]
    [MemberData(nameof(InboundScenarios))]
    public void ReceiveAndPutaway_tracks_scenario_identity(WorkflowScenario scenario)
    {
        var result = WorkflowSimulator.Execute(scenario, InboundFlow);
        result.ScenarioId.Should().Be(scenario.Id);
    }
}
