namespace LKvitai.MES.Tests.Warehouse.E2E;

public static class WorkflowSimulator
{
    public static WorkflowExecutionResult Execute(WorkflowScenario scenario, IReadOnlyList<string> expectedFlow)
    {
        var dbName = ParallelDatabaseAllocator.GetDatabaseName();
        var executed = new List<string>();

        foreach (var step in expectedFlow)
        {
            if (!scenario.Steps.Contains(step, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Scenario '{scenario.Id}' does not define expected step '{step}'.");
            }

            executed.Add(step);
        }

        return new WorkflowExecutionResult(
            scenario.Id,
            executed,
            scenario.Quantity,
            dbName);
    }
}
