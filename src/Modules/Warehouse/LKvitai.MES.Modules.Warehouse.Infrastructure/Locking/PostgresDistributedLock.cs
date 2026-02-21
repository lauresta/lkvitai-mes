using Microsoft.Extensions.Configuration;
using Npgsql;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Locking;

/// <summary>
/// PostgreSQL-backed distributed lock with TTL expiry.
/// Used for projection rebuild serialization when Redis is not configured.
/// </summary>
public sealed class PostgresDistributedLock : IDistributedLock
{
    private readonly string _connectionString;

    public PostgresDistributedLock(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("WarehouseDb")
            ?? throw new InvalidOperationException("WarehouseDb connection string not found");
    }

    public async Task<DistributedLockAcquireResult> TryAcquireAsync(
        string key,
        string holder,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureTableAsync(connection, cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // Expired rows are treated as stale lock holders and removed.
        await using (var cleanup = connection.CreateCommand())
        {
            cleanup.Transaction = transaction;
            cleanup.CommandText = @"
                DELETE FROM public.distributed_locks
                WHERE lock_key = @key
                  AND expires_at_utc <= NOW()";
            cleanup.Parameters.AddWithValue("key", key);
            await cleanup.ExecuteNonQueryAsync(cancellationToken);
        }

        var acquiredAt = DateTimeOffset.UtcNow;
        var expiresAt = acquiredAt.Add(ttl);

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = @"
                INSERT INTO public.distributed_locks (lock_key, holder, acquired_at_utc, expires_at_utc)
                VALUES (@key, @holder, @acquiredAtUtc, @expiresAtUtc)
                ON CONFLICT (lock_key) DO NOTHING";
            insert.Parameters.AddWithValue("key", key);
            insert.Parameters.AddWithValue("holder", holder);
            insert.Parameters.AddWithValue("acquiredAtUtc", acquiredAt);
            insert.Parameters.AddWithValue("expiresAtUtc", expiresAt);

            var inserted = await insert.ExecuteNonQueryAsync(cancellationToken);
            if (inserted == 1)
            {
                await transaction.CommitAsync(cancellationToken);
                return new DistributedLockAcquireResult(
                    true,
                    new DistributedLockInfo(key, holder, acquiredAt, expiresAt));
            }
        }

        DistributedLockInfo? existing;
        await using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = @"
                SELECT holder, acquired_at_utc, expires_at_utc
                FROM public.distributed_locks
                WHERE lock_key = @key";
            select.Parameters.AddWithValue("key", key);

            await using var reader = await select.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                existing = null;
            }
            else
            {
                existing = new DistributedLockInfo(
                    key,
                    reader.GetString(0),
                    reader.GetFieldValue<DateTimeOffset>(1),
                    reader.GetFieldValue<DateTimeOffset>(2));
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return new DistributedLockAcquireResult(false, existing);
    }

    public async Task ReleaseAsync(
        string key,
        string holder,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureTableAsync(connection, cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM public.distributed_locks
            WHERE lock_key = @key
              AND holder = @holder";
        cmd.Parameters.AddWithValue("key", key);
        cmd.Parameters.AddWithValue("holder", holder);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<DistributedLockInfo?> GetActiveLockAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await EnsureTableAsync(connection, cancellationToken);

        await using var cleanup = connection.CreateCommand();
        cleanup.CommandText = @"
            DELETE FROM public.distributed_locks
            WHERE lock_key = @key
              AND expires_at_utc <= NOW()";
        cleanup.Parameters.AddWithValue("key", key);
        await cleanup.ExecuteNonQueryAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT holder, acquired_at_utc, expires_at_utc
            FROM public.distributed_locks
            WHERE lock_key = @key";
        cmd.Parameters.AddWithValue("key", key);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new DistributedLockInfo(
            key,
            reader.GetString(0),
            reader.GetFieldValue<DateTimeOffset>(1),
            reader.GetFieldValue<DateTimeOffset>(2));
    }

    private static async Task EnsureTableAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS public.distributed_locks
            (
                lock_key         text PRIMARY KEY,
                holder           text                     NOT NULL,
                acquired_at_utc  timestamptz              NOT NULL,
                expires_at_utc   timestamptz              NOT NULL
            );";
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
