using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Application.Orchestration;

/// <summary>
/// Allocation orchestration interface.
/// Queries AvailableStock for candidates matching reservation lines,
/// then appends StockAllocatedEvent with SOFT lock to the reservation stream.
/// </summary>
public interface IAllocateReservationOrchestration
{
    /// <summary>
    /// Allocates available stock to the reservation.
    /// </summary>
    /// <param name="reservationId">Reservation to allocate stock for.</param>
    /// <param name="warehouseId">Warehouse to search for available stock.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure with domain error code.</returns>
    Task<Result> AllocateAsync(Guid reservationId, string warehouseId, CancellationToken ct = default);
}
