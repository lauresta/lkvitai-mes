using LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum.Accounting;

namespace LKvitai.MES.Modules.Warehouse.Api.BackgroundJobs;

/// <summary>Daily supplier sync from Agnum (blueprint 4.11).</summary>
public sealed class AgnumSupplierSyncRecurringJob
{
    public const string JobId = "agnum-supplier-sync-daily";

    private readonly AgnumSupplierSyncService _sync;
    private readonly ILogger<AgnumSupplierSyncRecurringJob> _logger;

    public AgnumSupplierSyncRecurringJob(AgnumSupplierSyncService sync, ILogger<AgnumSupplierSyncRecurringJob> logger)
    {
        _sync = sync;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var r = await _sync.SyncAsync(ct);
        _logger.LogInformation(
            "Agnum supplier sync: created {Created}, updated {Updated}, restored {Restored}, missing {Missing}, code conflicts {Conflicts}.",
            r.Created, r.Updated, r.Restored, r.MarkedMissing, r.CodeConflicts);
    }
}
