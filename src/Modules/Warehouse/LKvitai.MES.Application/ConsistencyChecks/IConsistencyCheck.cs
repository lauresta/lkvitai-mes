namespace LKvitai.MES.Application.ConsistencyChecks;

/// <summary>
/// Interface for consistency check services.
/// Detects data anomalies like orphan hard locks or stuck reservations.
/// </summary>
public interface IConsistencyCheck
{
    /// <summary>Name of this consistency check.</summary>
    string Name { get; }

    /// <summary>
    /// Executes the check and returns any detected anomalies.
    /// </summary>
    Task<IReadOnlyList<ConsistencyAnomaly>> CheckAsync(CancellationToken ct = default);
}

/// <summary>
/// Represents a detected consistency anomaly.
/// </summary>
public record ConsistencyAnomaly
{
    public string CheckName { get; init; } = string.Empty;
    public string ErrorCode { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Dictionary<string, object> Metadata { get; init; } = new();
    public DateTime DetectedAt { get; init; } = DateTime.UtcNow;
}
