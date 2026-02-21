using System.Text.Json.Serialization;

namespace LKvitai.MES.Tests.Warehouse.E2E;

public sealed class WorkflowScenario
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("steps")]
    public string[] Steps { get; set; } = Array.Empty<string>();

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
}
