using LKvitai.MES.Modules.Warehouse.Application.Ports;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace LKvitai.MES.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL advisory lock implementation of <see cref="IBalanceGuardLock"/>.
///
/// [HOTFIX CRIT-01] Uses pg_advisory_xact_lock on a dedicated connection to serialize
/// balance-affecting operations on the same (warehouseId, location, sku).
///
/// The lock is acquired within a transaction. CommitAsync commits the transaction,
/// releasing all advisory locks. DisposeAsync rolls back if not committed.
/// </summary>
public class PostgresBalanceGuardLock : IBalanceGuardLock
{
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;
    private bool _committed;

    internal PostgresBalanceGuardLock(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        _connection = connection;
        _transaction = transaction;
    }

    /// <inheritdoc />
    public async Task AcquireAsync(long[] sortedLockKeys, CancellationToken ct = default)
    {
        if (_connection is null || _transaction is null)
            throw new ObjectDisposedException(nameof(PostgresBalanceGuardLock));

        foreach (var key in sortedLockKeys)
        {
            await using var cmd = new NpgsqlCommand(
                "SELECT pg_advisory_xact_lock(@key)", _connection);
            cmd.Transaction = _transaction;
            cmd.Parameters.AddWithValue("key", key);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaction is not null && !_committed)
        {
            await _transaction.CommitAsync(ct);
            _committed = true;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            if (!_committed)
            {
                try { await _transaction.RollbackAsync(); }
                catch { /* best-effort cleanup */ }
            }
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}

/// <summary>
/// Factory that creates <see cref="PostgresBalanceGuardLock"/> instances.
/// Each instance gets its own Npgsql connection + transaction.
/// </summary>
public class PostgresBalanceGuardLockFactory : IBalanceGuardLockFactory
{
    private readonly string _connectionString;

    public PostgresBalanceGuardLockFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("WarehouseDb")
            ?? throw new InvalidOperationException(
                "WarehouseDb connection string not found in configuration.");
    }

    /// <inheritdoc />
    public async Task<IBalanceGuardLock> CreateAsync(CancellationToken ct = default)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        var transaction = await connection.BeginTransactionAsync(ct);
        return new PostgresBalanceGuardLock(connection, transaction);
    }
}
