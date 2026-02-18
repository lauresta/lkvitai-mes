using FluentAssertions;
using Xunit;

namespace LKvitai.MES.Tests.E2E;

public class CycleCountWorkflowTests
{
    private static readonly string[] CycleCountFlow = ["Schedule", "Execute", "ResolveDiscrepancy"];

    public static IEnumerable<object[]> CycleCountScenarios => WorkflowScenarioLoader.Load("cycle-count-scenarios.json");

    [Theory]
    [MemberData(nameof(CycleCountScenarios))]
    public void ScheduleExecuteResolve_completes_expected_steps(WorkflowScenario scenario)
    {
        var result = WorkflowSimulator.Execute(scenario, CycleCountFlow);
        result.ExecutedSteps.Should().ContainInOrder(CycleCountFlow);
    }

    [Theory]
    [MemberData(nameof(CycleCountScenarios))]
    public void ScheduleExecuteResolve_assigns_isolated_parallel_database(WorkflowScenario scenario)
    {
        var result = WorkflowSimulator.Execute(scenario, CycleCountFlow);
        result.DatabaseName.Should().MatchRegex("^test-db-[1-4]$");
    }

    [Theory]
    [MemberData(nameof(CycleCountScenarios))]
    public void ScheduleExecuteResolve_tracks_scenario_identity(WorkflowScenario scenario)
    {
        var result = WorkflowSimulator.Execute(scenario, CycleCountFlow);
        result.ScenarioId.Should().Be(scenario.Id);
    }
}

