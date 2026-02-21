using System.Text.Json;
using System.Text.Json.Serialization;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;

public record ProblemDetailsModel
{
    public string? Type { get; init; }
    public string? Title { get; init; }
    public int? Status { get; init; }
    public string? Detail { get; init; }
    public string? TraceId { get; init; }
    public Dictionary<string, string[]>? Errors { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extensions { get; init; }

    [JsonIgnore]
    public string? ErrorCode
    {
        get
        {
            if (Extensions is null || !Extensions.TryGetValue("errorCode", out var errorCode))
            {
                return null;
            }

            return errorCode.ValueKind switch
            {
                JsonValueKind.String => errorCode.GetString(),
                JsonValueKind.Null => null,
                _ => errorCode.GetRawText()
            };
        }
    }
}
