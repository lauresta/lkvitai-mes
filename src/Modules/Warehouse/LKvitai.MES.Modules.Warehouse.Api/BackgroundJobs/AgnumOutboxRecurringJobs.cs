using LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum.Accounting;

namespace LKvitai.MES.Modules.Warehouse.Api.BackgroundJobs;

/// <summary>Blueprint 4.7: sends the outbox to the Agnum service once a minute.</summary>
public sealed class AgnumOutboxRecurringJob
{
    public const string JobId = "agnum-outbox-sender";

    private readonly AgnumOutboxSender _sender;
    private readonly ILogger<AgnumOutboxRecurringJob> _logger;

    public AgnumOutboxRecurringJob(AgnumOutboxSender sender, ILogger<AgnumOutboxRecurringJob> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    [Hangfire.DisableConcurrentExecution(120)]
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var r = await _sender.RunOnceAsync(ct);
        if (r.Sent + r.Retry + r.Review > 0)
            _logger.LogInformation("Agnum outbox: sent {Sent}, to retry {Retry}, needs review {Review}, skipped {Skipped}.", r.Sent, r.Retry, r.Review, r.Skipped);
    }
}

/// <summary>Blueprint 4.7 nightly reconciliation against the by-period route of the service.</summary>
public sealed class AgnumReconciliationRecurringJob
{
    public const string JobId = "agnum-outbox-reconciliation";

    private readonly AgnumOutboxReconciler _reconciler;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AgnumReconciliationRecurringJob> _logger;

    public AgnumReconciliationRecurringJob(AgnumOutboxReconciler reconciler, IConfiguration configuration, ILogger<AgnumReconciliationRecurringJob> logger)
    {
        _reconciler = reconciler;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var days = _configuration.GetValue("Agnum:Sending:ReconcileDays", AgnumOutboxReconciler.DefaultDays);
        var findings = await _reconciler.RunAsync(days, ct);
        _logger.LogInformation("Agnum reconciliation finished with {Findings} findings (-1: service unavailable).", findings);
    }
}
