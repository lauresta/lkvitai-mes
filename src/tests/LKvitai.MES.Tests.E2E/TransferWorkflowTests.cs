using FluentAssertions;
using Xunit;

namespace LKvitai.MES.Tests.E2E;

public class TransferWorkflowTests
{
    private static readonly string[] TransferFlow = ["CreateTransfer", "ApproveTransfer", "ExecuteTransfer"];

    public static IEnumerable<object[]> TransferScenarios => WorkflowScenarioLoader.Load("transfer-scenarios.json");

    [Theory]
    [MemberData(nameof(TransferScenarios))]
    public void CreateApproveExecute_completes_expected_steps(WorkflowScenario scenario)
    {
        var result = WorkflowSimulator.Execute(scenario, TransferFlow);
        result.ExecutedSteps.Should().ContainInOrder(TransferFlow);
    }

    [Theory]
    [MemberData(nameof(TransferScenarios))]
    public void CreateApproveExecute_assigns_isolated_parallel_database(WorkflowScenario scenario)
    {
        var result = WorkflowSimulator.Execute(scenario, TransferFlow);
        result.DatabaseName.Should().MatchRegex("^test-db-[1-4]$");
    }

    [Theory]
    [MemberData(nameof(TransferScenarios))]
    public void CreateApproveExecute_tracks_scenario_identity(WorkflowScenario scenario)
    {
        var result = WorkflowSimulator.Execute(scenario, TransferFlow);
        result.ScenarioId.Should().Be(scenario.Id);
    }
}

