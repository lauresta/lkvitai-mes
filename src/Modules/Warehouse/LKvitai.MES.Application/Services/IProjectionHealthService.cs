namespace LKvitai.MES.Application.Services;

public interface IProjectionHealthService
{
    Task<ProjectionHealthSnapshot> GetHealthAsync(CancellationToken cancellationToken = default);
}

public sealed record ProjectionHealthSnapshot(
    string Status,
    string DatabaseStatus,
    string EventStoreStatus,
    string ProjectionLagStatus,
    DateTimeOffset CheckedAt,
    IReadOnlyDictionary<string, ProjectionHealthItem> ProjectionStatus);

public sealed record ProjectionHealthItem(
    string ProjectionName,
    long? HighWaterMark,
    long? LastProcessed,
    long? LagEvents,
    double? LagSeconds,
    DateTimeOffset? LastUpdated,
    string Status);
