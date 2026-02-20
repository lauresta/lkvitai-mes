using LKvitai.MES.Application.Services;
using Marten;
using Npgsql;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Infrastructure.Projections;

public sealed class ProjectionHealthService : IProjectionHealthService
{
    private readonly IDocumentStore _documentStore;
    private readonly ILogger<ProjectionHealthService> _logger;

    public ProjectionHealthService(
        IDocumentStore documentStore,
        ILogger<ProjectionHealthService> logger)
    {
        _documentStore = documentStore;
        _logger = logger;
    }

    public async Task<ProjectionHealthSnapshot> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var databaseStatus = "Healthy";
        var eventStoreStatus = "Healthy";

        try
        {
            await using var session = _documentStore.QuerySession();
            _ = await session.Query<Contracts.ReadModels.AvailableStockView>()
                .Take(1)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Projection health database probe failed");
            databaseStatus = "Unhealthy";
            eventStoreStatus = "Unhealthy";
        }

        var allProgress = await _documentStore.Advanced.AllProjectionProgress(token: cancellationToken);
        var eventStoreStatistics = await _documentStore.Advanced.FetchEventStoreStatistics(token: cancellationToken);
        var highWaterMark = (long?)eventStoreStatistics.EventSequenceNumber;

        var progressRows = allProgress
            .Select(progress => new
            {
                ProjectionName = ResolveProjectionName(progress.ShardName),
                LastProcessed = (long?)progress.Sequence
            })
            .Where(progress => !string.IsNullOrWhiteSpace(progress.ProjectionName))
            .GroupBy(progress => progress.ProjectionName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                ProjectionName = group.Key,
                LastProcessed = group.Max(x => x.LastProcessed)
            })
            .OrderBy(x => x.ProjectionName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        await using var querySession = _documentStore.QuerySession();
        var conn = (NpgsqlConnection?)querySession.Connection;

        var projectionItems = new Dictionary<string, ProjectionHealthItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in progressRows)
        {
            long? lagEvents = highWaterMark.HasValue && row.LastProcessed.HasValue
                ? Math.Max(0, highWaterMark.Value - row.LastProcessed.Value)
                : null;

            var lastUpdated = row.LastProcessed.HasValue
                ? await GetEventTimestampAsync(conn, row.LastProcessed.Value, cancellationToken)
                : null;

            // If projection has caught up to high-water mark, logical lag is zero
            // even if no new events were produced for a long time.
            double? lagSeconds = lagEvents == 0
                ? 0d
                : lastUpdated.HasValue
                    ? Math.Max(0d, (checkedAt - lastUpdated.Value).TotalSeconds)
                    : null;

            var status = ClassifyStatus(lagSeconds);

            projectionItems[row.ProjectionName] = new ProjectionHealthItem(
                row.ProjectionName,
                highWaterMark,
                row.LastProcessed,
                lagEvents,
                lagSeconds,
                lastUpdated,
                status);
        }

        var projectionLagStatus = projectionItems.Count == 0
            ? "Healthy"
            : projectionItems.Values.Any(x => x.Status == "Unhealthy")
                ? "Unhealthy"
                : projectionItems.Values.Any(x => x.Status == "Degraded")
                    ? "Degraded"
                    : "Healthy";

        var overallStatus = databaseStatus == "Unhealthy" || eventStoreStatus == "Unhealthy" || projectionLagStatus == "Unhealthy"
            ? "Degraded"
            : projectionLagStatus == "Degraded"
                ? "Degraded"
                : "Healthy";

        return new ProjectionHealthSnapshot(
            overallStatus,
            databaseStatus,
            eventStoreStatus,
            projectionLagStatus,
            checkedAt,
            projectionItems);
    }

    public static string ClassifyStatus(double? lagSeconds)
    {
        if (!lagSeconds.HasValue)
        {
            return "Healthy";
        }

        if (lagSeconds.Value < 1d)
        {
            return "Healthy";
        }

        if (lagSeconds.Value <= 60d)
        {
            return "Degraded";
        }

        return "Unhealthy";
    }

    private static string ResolveProjectionName(string? shardName)
    {
        if (string.IsNullOrWhiteSpace(shardName))
        {
            return string.Empty;
        }

        var separatorIndex = shardName.IndexOf(':');
        if (separatorIndex <= 0)
        {
            return shardName;
        }

        return shardName[..separatorIndex];
    }

    private static async Task<DateTimeOffset?> GetEventTimestampAsync(
        NpgsqlConnection? conn,
        long sequence,
        CancellationToken cancellationToken)
    {
        if (conn is null)
        {
            return null;
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT ""timestamp""
            FROM warehouse_events.mt_events
            WHERE seq_id = @seq
            LIMIT 1";
        cmd.Parameters.AddWithValue("seq", sequence);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
            _ => null
        };
    }
}
