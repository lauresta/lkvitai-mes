namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public sealed class ScheduledReportsRecurringJob
{
    private readonly IComplianceReportService _service;
    private readonly ILogger<ScheduledReportsRecurringJob> _logger;

    public ScheduledReportsRecurringJob(IComplianceReportService service, ILogger<ScheduledReportsRecurringJob> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        var processed = await _service.ProcessDueSchedulesAsync();
        _logger.LogInformation("Processed {Count} scheduled compliance reports", processed);
    }
}
