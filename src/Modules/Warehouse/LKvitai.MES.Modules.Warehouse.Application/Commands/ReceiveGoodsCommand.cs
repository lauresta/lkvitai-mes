using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Application.Commands;

/// <summary>
/// Command to receive goods from a supplier.
/// Minimal Phase 1 implementation of ReceiveGoodsSaga (Req 15.1-15.9).
///
/// Workflow (single transaction boundary):
///   1. Emit HandlingUnitCreated event (HU stream)
///   2. Record StockMovement per line (SUPPLIER → location) via StockLedger
///   3. Emit HandlingUnitSealed event (HU stream)
///   All committed atomically — no partial state on failure.
/// </summary>
public record ReceiveGoodsCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    public string WarehouseId { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string HuType { get; init; } = "PALLET"; // PALLET, BOX, BAG, UNIT
    public Guid OperatorId { get; init; }

    /// <summary>
    /// Lines to receive. Each line becomes a StockMoved event (SUPPLIER → Location).
    /// </summary>
    public List<ReceiveGoodsLineDto> Lines { get; init; } = new();
}

public record ReceiveGoodsLineDto
{
    public string SKU { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
}
