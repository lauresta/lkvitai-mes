namespace LKvitai.MES.Modules.Warehouse.Infrastructure.BackgroundJobs;

public interface IIdempotencyCleanupService
{
    Task<int> CleanupAsync(CancellationToken cancellationToken = default);
}
