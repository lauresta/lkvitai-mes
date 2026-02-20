using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Infrastructure.BackgroundJobs;

/// <summary>
/// Daily idempotency cleanup at 02:00 UTC.
/// </summary>
public sealed class IdempotencyCleanupHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(15);

    private readonly IIdempotencyCleanupService _cleanupService;
    private readonly ILogger<IdempotencyCleanupHostedService> _logger;
    private DateOnly? _lastRunDate;

    public IdempotencyCleanupHostedService(
        IIdempotencyCleanupService cleanupService,
        ILogger<IdempotencyCleanupHostedService> logger)
    {
        _cleanupService = cleanupService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunIfDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Idempotency cleanup execution failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task RunIfDueAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        if (now.Hour != 2)
        {
            return;
        }

        var today = DateOnly.FromDateTime(now);
        if (_lastRunDate == today)
        {
            return;
        }

        var deletedCount = await _cleanupService.CleanupAsync(cancellationToken);
        _lastRunDate = today;

        _logger.LogInformation(
            "Idempotency cleanup completed: {DeletedCount} records removed",
            deletedCount);
    }
}
