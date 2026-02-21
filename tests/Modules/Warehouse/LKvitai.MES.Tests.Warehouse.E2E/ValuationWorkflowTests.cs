using FluentAssertions;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.E2E;

public class ValuationWorkflowTests
{
    private static readonly string[] ValuationFlow = ["AdjustCost", "AllocateLandedCost", "WriteDown"];

    public static IEnumerable<object[]> ValuationScenarios => WorkflowScenarioLoader.Load("valuation-scenarios.json");

    [Theory]
    [MemberData(nameof(ValuationScenarios))]
    public void CostAdjustmentFlow_completes_expected_steps(WorkflowScenario scenario)
    {
        var result = WorkflowSimulator.Execute(scenario, ValuationFlow);
        result.ExecutedSteps.Should().ContainInOrder(ValuationFlow);
    }

    [Theory]
    [MemberData(nameof(ValuationScenarios))]
    public void CostAdjustmentFlow_assigns_isolated_parallel_database(WorkflowScenario scenario)
    {
        var result = WorkflowSimulator.Execute(scenario, ValuationFlow);
        result.DatabaseName.Should().MatchRegex("^test-db-[1-4]$");
    }

    [Theory]
    [MemberData(nameof(ValuationScenarios))]
    public void CostAdjustmentFlow_maintains_positive_quantity(WorkflowScenario scenario)
    {
        var result = WorkflowSimulator.Execute(scenario, ValuationFlow);
        result.FinalQuantity.Should().BeGreaterThan(0);
    }

    [Theory]
    [MemberData(nameof(ValuationScenarios))]
    public void CostAdjustmentFlow_tracks_scenario_identity(WorkflowScenario scenario)
    {
        var result = WorkflowSimulator.Execute(scenario, ValuationFlow);
        result.ScenarioId.Should().Be(scenario.Id);
    }
}

