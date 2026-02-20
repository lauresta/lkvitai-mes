using LKvitai.MES.Modules.Warehouse.Application.Ports;
using Marten;

namespace LKvitai.MES.Infrastructure.BackgroundJobs;

public sealed class IdempotencyCleanupService : IIdempotencyCleanupService
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    private readonly IDocumentStore _documentStore;

    public IdempotencyCleanupService(IDocumentStore documentStore)
    {
        _documentStore = documentStore;
    }

    public async Task<int> CleanupAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.Subtract(Retention);

        await using var querySession = _documentStore.QuerySession();
        var deletedCount = await Marten.QueryableExtensions.CountAsync(
            querySession.Query<ProcessedCommandRecord>().Where(x => x.CreatedAt < cutoff),
            cancellationToken);

        if (deletedCount == 0)
        {
            return 0;
        }

        await using var session = _documentStore.LightweightSession();
        session.DeleteWhere<ProcessedCommandRecord>(x => x.CreatedAt < cutoff);
        await session.SaveChangesAsync(cancellationToken);

        return deletedCount;
    }
}
