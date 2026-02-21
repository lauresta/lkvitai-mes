namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Locking;

public interface IDistributedLock
{
    Task<DistributedLockAcquireResult> TryAcquireAsync(
        string key,
        string holder,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);

    Task ReleaseAsync(
        string key,
        string holder,
        CancellationToken cancellationToken = default);

    Task<DistributedLockInfo?> GetActiveLockAsync(
        string key,
        CancellationToken cancellationToken = default);
}

public sealed record DistributedLockAcquireResult(
    bool Acquired,
    DistributedLockInfo? ExistingLock);

public sealed record DistributedLockInfo(
    string Key,
    string Holder,
    DateTimeOffset AcquiredAtUtc,
    DateTimeOffset ExpiresAtUtc);
