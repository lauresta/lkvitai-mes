namespace LKvitai.MES.Infrastructure.BackgroundJobs;

public interface IIdempotencyCleanupService
{
    Task<int> CleanupAsync(CancellationToken cancellationToken = default);
}
