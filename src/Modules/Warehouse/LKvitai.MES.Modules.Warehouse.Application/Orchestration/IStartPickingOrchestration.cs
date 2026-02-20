using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Application.Orchestration;

/// <summary>
/// StartPicking orchestration interface
/// [MITIGATION R-3] Defines contract for atomic HARD lock acquisition
/// </summary>
public interface IStartPickingOrchestration
{
    /// <summary>
    /// Orchestrates StartPicking workflow with atomic HARD lock acquisition
    /// </summary>
    /// <param name="reservationId">Reservation to transition to HARD lock</param>
    /// <param name="operatorId">Operator starting picking</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    Task<Result> StartPickingAsync(
        Guid reservationId,
        Guid operatorId,
        CancellationToken cancellationToken = default);
}
