using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Application.Projections;

/// <summary>
/// Projection rebuild service interface
/// [MITIGATION V-5] Defines contract for deterministic projection rebuild
/// 
/// Rebuild Contract per design document:
/// - Rule A: Stream-ordered replay (by sequence number, not timestamp)
/// - Rule B: Self-contained event data (no external queries)
/// - Rule C: Rebuild verification gate (shadow table + checksum)
/// </summary>
public interface IProjectionRebuildService
{
    /// <summary>
    /// Rebuilds projection from event stream using shadow table approach
    /// </summary>
    /// <param name="projectionName">Name of projection to rebuild</param>
    /// <param name="verify">Whether to verify checksum before swapping</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result with rebuild report</returns>
    Task<Result<Commands.ProjectionRebuildReport>> RebuildProjectionAsync(
        string projectionName,
        bool verify = true,
        bool resetProgress = false,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generates diff report between production and shadow projection
    /// </summary>
    /// <param name="projectionName">Name of projection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Diff report</returns>
    Task<ProjectionDiffReport> GenerateDiffReportAsync(
        string projectionName,
        CancellationToken cancellationToken = default);

    Task<ProjectionRebuildLockStatus?> GetRebuildStatusAsync(
        string projectionName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Projection diff report
/// </summary>
public record ProjectionDiffReport
{
    public string ProjectionName { get; init; } = string.Empty;
    public int RowsOnlyInProduction { get; init; }
    public int RowsOnlyInShadow { get; init; }
    public int RowsWithDifferences { get; init; }
    public List<string> SampleDifferences { get; init; } = new();
}

public sealed record ProjectionRebuildLockStatus
{
    public string ProjectionName { get; init; } = string.Empty;
    public bool Locked { get; init; }
    public string? Holder { get; init; }
    public DateTimeOffset? AcquiredAtUtc { get; init; }
    public DateTimeOffset? ExpiresAtUtc { get; init; }
}
