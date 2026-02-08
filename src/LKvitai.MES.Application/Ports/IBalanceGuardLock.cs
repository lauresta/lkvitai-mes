namespace LKvitai.MES.Application.Ports;

/// <summary>
/// Application port for acquiring advisory locks that serialize balance-affecting operations.
///
/// [HOTFIX CRIT-01] Ensures hardLockedSum(location,sku) never exceeds StockLedger balance.
/// Both StartPicking and outbound stock movements must hold this lock while reading balance
/// and appending events to prevent concurrent balance reduction during HARD lock acquisition.
///
/// The lock is scoped to a PostgreSQL transaction and automatically released when disposed.
/// Lock keys are derived from StockLockKey.ForLocation(warehouseId, location, sku).
/// </summary>
public interface IBalanceGuardLock : IAsyncDisposable
{
    /// <summary>
    /// Acquires pg_advisory_xact_lock for the given lock keys.
    /// Keys MUST be sorted before calling (use StockLockKey.ForLocations for sorting).
    /// Blocks until all locks are acquired.
    /// Dispose releases the locks (commits the advisory lock transaction).
    /// </summary>
    Task AcquireAsync(long[] sortedLockKeys, CancellationToken ct = default);

    /// <summary>
    /// Commits the advisory lock transaction and releases all held locks.
    /// Call this after the Marten session has been committed, so the next
    /// serialized session sees all committed data under READ COMMITTED.
    /// </summary>
    Task CommitAsync(CancellationToken ct = default);
}

/// <summary>
/// Factory for creating balance guard lock instances.
/// Each instance manages its own PostgreSQL connection + transaction.
/// </summary>
public interface IBalanceGuardLockFactory
{
    /// <summary>
    /// Creates a new lock instance with its own connection.
    /// The caller must dispose the instance after use.
    /// </summary>
    Task<IBalanceGuardLock> CreateAsync(CancellationToken ct = default);
}
