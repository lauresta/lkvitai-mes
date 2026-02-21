using LKvitai.MES.BuildingBlocks.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Application.Orchestration;

/// <summary>
/// PickStock orchestration port.
///
/// [MITIGATION V-3] StockMovement is recorded FIRST.
/// HU projection updates asynchronously (NOT waited on).
/// Reservation consumption is independent of HU projection status.
/// </summary>
public interface IPickStockOrchestration
{
    /// <summary>
    /// Executes the full PickStock workflow:
    ///   1. Validate reservation is PICKING (HARD locked)
    ///   2. Record StockMovement (V-3: StockLedger FIRST)
    ///   3. Consume reservation
    /// </summary>
    /// <returns>
    /// On full success: Result with the movement ID.
    /// On movement success + consumption failure: PickStockResult with MovementCommitted=true
    ///   and ConsumptionError set (caller must defer to saga).
    /// On movement failure: Result.Fail with error code.
    /// </returns>
    Task<PickStockResult> ExecuteAsync(
        Guid reservationId,
        Guid handlingUnitId,
        string warehouseId,
        string sku,
        decimal quantity,
        string fromLocation,
        Guid operatorId,
        CancellationToken ct = default);

    /// <summary>
    /// Attempts only the reservation consumption step (used by saga retry).
    /// Called when movement was already committed but consumption failed.
    /// </summary>
    Task<Result> ConsumeReservationAsync(
        Guid reservationId,
        decimal quantity,
        CancellationToken ct = default);
}

/// <summary>
/// Result of the PickStock orchestration that distinguishes partial success
/// (movement committed but consumption failed).
/// </summary>
public class PickStockResult
{
    public bool IsSuccess { get; init; }
    public bool MovementCommitted { get; init; }
    public Guid MovementId { get; init; }
    public string? Error { get; init; }

    public static PickStockResult Ok(Guid movementId) => new()
    {
        IsSuccess = true,
        MovementCommitted = true,
        MovementId = movementId
    };

    public static PickStockResult MovementFailed(string error) => new()
    {
        IsSuccess = false,
        MovementCommitted = false,
        Error = error
    };

    public static PickStockResult ConsumptionDeferred(Guid movementId, string error) => new()
    {
        IsSuccess = false,
        MovementCommitted = true,
        MovementId = movementId,
        Error = error
    };
}
