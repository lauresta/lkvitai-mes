using LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum.Accounting;

namespace LKvitai.MES.Modules.Warehouse.Api.BackgroundJobs;

/// <summary>Daily supplier sync from Agnum (blueprint 4.11).</summary>
public sealed class AgnumSupplierSyncRecurringJob
{
    public const string JobId = "agnum-supplier-sync-daily";

    /// <summary>
    /// On by default. Production sets it to false while Agnum is not connected there: the job would otherwise
    /// call the placeholder Agnum address every morning and end as a failed Hangfire job after all retries.
    /// </summary>
    public const string EnabledSetting = "Agnum:SupplierSync:Enabled";

    private readonly AgnumSupplierSyncService _sync;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AgnumSupplierSyncRecurringJob> _logger;

    public AgnumSupplierSyncRecurringJob(
        AgnumSupplierSyncService sync,
        IConfiguration configuration,
        ILogger<AgnumSupplierSyncRecurringJob> logger)
    {
        _sync = sync;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        if (!_configuration.GetValue(EnabledSetting, true))
        {
            _logger.LogInformation("Agnum supplier sync skipped: {Setting} is false.", EnabledSetting);
            return;
        }

        var r = await _sync.SyncAsync(ct);
        _logger.LogInformation(
            "Agnum supplier sync: created {Created}, updated {Updated}, restored {Restored}, missing {Missing}, code conflicts {Conflicts}.",
            r.Created, r.Updated, r.Restored, r.MarkedMissing, r.CodeConflicts);
    }
}
