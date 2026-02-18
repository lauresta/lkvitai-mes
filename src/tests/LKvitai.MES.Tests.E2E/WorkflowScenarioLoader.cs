using System.Text.Json;

namespace LKvitai.MES.Tests.E2E;

public static class WorkflowScenarioLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IEnumerable<object[]> Load(string relativePath)
    {
        var basePath = AppContext.BaseDirectory;
        var fullPath = Path.Combine(basePath, "tests", "data", relativePath);
        var json = File.ReadAllText(fullPath);
        var scenarios = JsonSerializer.Deserialize<List<WorkflowScenario>>(json, Options)
            ?? new List<WorkflowScenario>();

        return scenarios.Select(s => new object[] { s });
    }
}
