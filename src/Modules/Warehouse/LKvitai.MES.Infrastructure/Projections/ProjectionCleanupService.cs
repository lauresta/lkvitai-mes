using LKvitai.MES.Infrastructure.Locking;
using Marten;
using Npgsql;

namespace LKvitai.MES.Infrastructure.Projections;

public interface IProjectionCleanupService
{
    Task<ProjectionCleanupResult> CleanupShadowTablesAsync(CancellationToken cancellationToken = default);
}

public sealed record ProjectionCleanupResult(
    int DroppedTables,
    IReadOnlyList<string> ShadowTables,
    string? Reason);

/// <summary>
/// Detects and removes orphaned Marten projection shadow tables.
/// Cleanup is skipped while an active rebuild lock exists.
/// </summary>
public sealed class ProjectionCleanupService : IProjectionCleanupService
{
    private readonly IDocumentStore _documentStore;
    private readonly IDistributedLock _distributedLock;

    public ProjectionCleanupService(
        IDocumentStore documentStore,
        IDistributedLock distributedLock)
    {
        _documentStore = documentStore;
        _distributedLock = distributedLock;
    }

    public async Task<ProjectionCleanupResult> CleanupShadowTablesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var session = _documentStore.QuerySession();
        var conn = (NpgsqlConnection)(session.Connection
            ?? throw new InvalidOperationException("Marten query session connection is unavailable."));

        var shadowTables = await GetShadowTablesAsync(conn, cancellationToken);
        if (shadowTables.Count == 0)
        {
            return new ProjectionCleanupResult(0, Array.Empty<string>(), null);
        }

        foreach (var projection in KnownProjectionNames)
        {
            var key = ProjectionRebuildLockKey.For(projection);
            var activeLock = await _distributedLock.GetActiveLockAsync(key, cancellationToken);
            if (activeLock is not null)
            {
                return new ProjectionCleanupResult(
                    0,
                    shadowTables,
                    $"Cleanup skipped: rebuild lock held by '{activeLock.Holder}' for projection '{projection}' until {activeLock.ExpiresAtUtc:O}.");
            }
        }

        var dropped = 0;
        foreach (var table in shadowTables)
        {
            await using var drop = conn.CreateCommand();
            drop.CommandText = $"DROP TABLE IF EXISTS {table} CASCADE";
            dropped += await drop.ExecuteNonQueryAsync(cancellationToken) > 0 ? 1 : 0;
        }

        return new ProjectionCleanupResult(dropped, shadowTables, null);
    }

    private static async Task<List<string>> GetShadowTablesAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        var tables = new List<string>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT quote_ident(schemaname) || '.' || quote_ident(tablename)
            FROM pg_tables
            WHERE tablename LIKE 'mt_doc\_%\_shadow' ESCAPE '\'
            ORDER BY schemaname, tablename";

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private static readonly string[] KnownProjectionNames = ["LocationBalance", "AvailableStock", "OnHandValue"];
}
