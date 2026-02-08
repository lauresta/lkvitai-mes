using LKvitai.MES.Application.Ports;
using LKvitai.MES.SharedKernel;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Application.ConsistencyChecks;

/// <summary>
/// Detects orphan hard locks — entries in the ActiveHardLocks projection
/// whose reservation is no longer in PICKING state (consumed, cancelled, etc.).
///
/// This can happen when the PickStock saga fails permanently:
/// the StockMovement is committed but the reservation is not consumed,
/// leaving stale hard locks in the projection.
/// </summary>
public class OrphanHardLockCheck : IConsistencyCheck
{
    private readonly IActiveHardLocksRepository _hardLocksRepository;
    private readonly IReservationRepository _reservationRepository;
    private readonly ILogger<OrphanHardLockCheck> _logger;

    public string Name => "OrphanHardLockCheck";

    public OrphanHardLockCheck(
        IActiveHardLocksRepository hardLocksRepository,
        IReservationRepository reservationRepository,
        ILogger<OrphanHardLockCheck> logger)
    {
        _hardLocksRepository = hardLocksRepository;
        _reservationRepository = reservationRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ConsistencyAnomaly>> CheckAsync(CancellationToken ct = default)
    {
        var anomalies = new List<ConsistencyAnomaly>();

        try
        {
            var activeHardLocks = await _hardLocksRepository.GetAllActiveLocksAsync(ct);

            foreach (var hardLock in activeHardLocks)
            {
                var reservationState = await _reservationRepository
                    .GetReservationStatusAsync(hardLock.ReservationId, ct);

                if (reservationState is null)
                {
                    anomalies.Add(new ConsistencyAnomaly
                    {
                        CheckName = Name,
                        ErrorCode = DomainErrorCodes.OrphanHardLockDetected,
                        Description = $"Hard lock for reservation {hardLock.ReservationId} exists " +
                                      "but the reservation was not found",
                        Metadata = new Dictionary<string, object>
                        {
                            ["ReservationId"] = hardLock.ReservationId,
                            ["WarehouseId"] = hardLock.WarehouseId,
                            ["Location"] = hardLock.Location,
                            ["SKU"] = hardLock.SKU
                        }
                    });
                }
                else if (reservationState != "PICKING")
                {
                    anomalies.Add(new ConsistencyAnomaly
                    {
                        CheckName = Name,
                        ErrorCode = DomainErrorCodes.OrphanHardLockDetected,
                        Description = $"Hard lock for reservation {hardLock.ReservationId} exists " +
                                      $"but reservation is in state '{reservationState}' (not PICKING)",
                        Metadata = new Dictionary<string, object>
                        {
                            ["ReservationId"] = hardLock.ReservationId,
                            ["ReservationState"] = reservationState,
                            ["WarehouseId"] = hardLock.WarehouseId,
                            ["Location"] = hardLock.Location,
                            ["SKU"] = hardLock.SKU
                        }
                    });

                    _logger.LogWarning(
                        "[CONSISTENCY] Orphan hard lock detected: Reservation {ReservationId} " +
                        "in state '{State}' but hard lock exists at {Warehouse}/{Location}/{SKU}",
                        hardLock.ReservationId, reservationState,
                        hardLock.WarehouseId, hardLock.Location, hardLock.SKU);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CONSISTENCY] OrphanHardLockCheck failed");
        }

        return anomalies;
    }
}
