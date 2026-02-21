using LKvitai.MES.Contracts.ReadModels;
using Marten;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.BackgroundJobs;

/// <summary>
/// Marks expired active reservations in projection storage.
/// </summary>
public sealed class ReservationExpiryJob : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    private readonly IDocumentStore _documentStore;
    private readonly ILogger<ReservationExpiryJob> _logger;

    public ReservationExpiryJob(IDocumentStore documentStore, ILogger<ReservationExpiryJob> logger)
    {
        _documentStore = documentStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ExpireReservationsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reservation expiry sweep failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task ExpireReservationsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        await using var session = _documentStore.LightweightSession();

        var expired = await session.Query<ActiveReservationView>()
            .Where(x => x.Status == "Active" && x.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
        {
            return;
        }

        foreach (var reservation in expired)
        {
            reservation.Status = "Expired";
            reservation.LastUpdated = now;
            session.Store(reservation);
        }

        await session.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Marked {Count} reservations as expired",
            expired.Count);
    }
}
