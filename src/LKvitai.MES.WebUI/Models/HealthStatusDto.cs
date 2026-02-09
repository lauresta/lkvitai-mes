using System.Text.Json.Serialization;

namespace LKvitai.MES.WebUI.Models;

public record HealthStatusDto
{
    public string Status { get; init; } = string.Empty;
    public double? ProjectionLag { get; init; }
    public DateTime? LastCheck { get; init; }

    // Backward-compatible fields for the current API payload.
    public bool Ok { get; init; }
    public string Service { get; init; } = string.Empty;
    public string Version { get; init; } = "dev";
    public DateTime? UtcNow { get; init; }

    [JsonIgnore]
    public string EffectiveStatus =>
        !string.IsNullOrWhiteSpace(Status)
            ? Status
            : (Ok ? "healthy" : "degraded");

    [JsonIgnore]
    public DateTime? EffectiveLastCheck => LastCheck ?? UtcNow;
}
