namespace LKvitai.MES.Tests.Warehouse.E2E;

public sealed record WorkflowExecutionResult(
    string ScenarioId,
    IReadOnlyList<string> ExecutedSteps,
    int FinalQuantity,
    string DatabaseName);
