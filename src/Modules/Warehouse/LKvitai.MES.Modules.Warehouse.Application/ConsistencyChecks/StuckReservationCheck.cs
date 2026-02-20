using LKvitai.MES.Application.Ports;
using LKvitai.MES.SharedKernel;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Application.ConsistencyChecks;

/// <summary>
/// Detects reservations stuck in PICKING state beyond the timeout threshold.
///
/// Per blueprint: 2-hour PICKING/HARD timeout policy.
/// Reservations in PICKING state for longer than the threshold
/// are flagged as stuck for supervisor intervention.
/// </summary>
public class StuckReservationCheck : IConsistencyCheck
{
    private readonly IReservationRepository _reservationRepository;
    private readonly ILogger<StuckReservationCheck> _logger;

    /// <summary>
    /// PICKING/HARD timeout threshold (2 hours per blueprint spec).
    /// </summary>
    public static readonly TimeSpan PickingTimeout = TimeSpan.FromHours(2);

    public string Name => "StuckReservationCheck";

    public StuckReservationCheck(
        IReservationRepository reservationRepository,
        ILogger<StuckReservationCheck> logger)
    {
        _reservationRepository = reservationRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ConsistencyAnomaly>> CheckAsync(CancellationToken ct = default)
    {
        var anomalies = new List<ConsistencyAnomaly>();

        try
        {
            var pickingReservations = await _reservationRepository
                .GetReservationsInStateAsync("PICKING", ct);

            var cutoff = DateTime.UtcNow - PickingTimeout;

            foreach (var reservation in pickingReservations)
            {
                // Check if the reservation has been in PICKING state too long
                if (reservation.PickingStartedAt.HasValue && reservation.PickingStartedAt.Value < cutoff)
                {
                    var stuckDuration = DateTime.UtcNow - reservation.PickingStartedAt.Value;

                    anomalies.Add(new ConsistencyAnomaly
                    {
                        CheckName = Name,
                        ErrorCode = DomainErrorCodes.StuckReservationDetected,
                        Description = $"Reservation {reservation.ReservationId} stuck in PICKING state " +
                                      $"for {stuckDuration.TotalMinutes:F0} minutes (threshold: {PickingTimeout.TotalMinutes:F0} min)",
                        Metadata = new Dictionary<string, object>
                        {
                            ["ReservationId"] = reservation.ReservationId,
                            ["PickingStartedAt"] = reservation.PickingStartedAt.Value,
                            ["StuckDurationMinutes"] = stuckDuration.TotalMinutes
                        }
                    });

                    _logger.LogWarning(
                        "[CONSISTENCY] Stuck reservation detected: {ReservationId} in PICKING for {Duration} min",
                        reservation.ReservationId, stuckDuration.TotalMinutes);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CONSISTENCY] StuckReservationCheck failed");
        }

        return anomalies;
    }
}
