namespace LKvitai.MES.Tests.E2E;

public sealed record WorkflowExecutionResult(
    string ScenarioId,
    IReadOnlyList<string> ExecutedSteps,
    int FinalQuantity,
    string DatabaseName);
