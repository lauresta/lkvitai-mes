using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Application.Orchestration;

/// <summary>
/// Port for the ReceiveGoods workflow orchestration.
/// Application layer owns this interface; Infrastructure provides the Marten implementation.
///
/// The orchestration appends events to multiple streams (StockLedger + HandlingUnit)
/// within a single transaction to ensure atomicity (no partial commits).
/// </summary>
public interface IReceiveGoodsOrchestration
{
    /// <summary>
    /// Executes the ReceiveGoods workflow:
    ///   1. Creates HandlingUnit (HU lifecycle events)
    ///   2. Records StockMovement per line (StockLedger events)
    ///   3. Seals HandlingUnit
    /// All in one transaction.
    /// </summary>
    /// <returns>
    /// On success: Result with the created HU ID.
    /// On failure: Result with error message.
    /// </returns>
    Task<Result<Guid>> ExecuteAsync(ReceiveGoodsCommand command, CancellationToken ct);
}
