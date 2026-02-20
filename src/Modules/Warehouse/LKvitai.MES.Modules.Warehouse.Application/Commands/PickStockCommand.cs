using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Application.Commands;

/// <summary>
/// Command to pick stock against a HARD-locked reservation.
///
/// [MITIGATION V-3] Transaction ordering:
///   1. Record StockMovement (StockLedger FIRST)
///   2. Consume reservation (independent of HU projection)
///   3. HU projection updates asynchronously (NOT waited on)
///
/// If reservation consumption fails after movement is committed,
/// the handler defers consumption to the MassTransit saga for durable retry.
/// </summary>
public record PickStockCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    public Guid ReservationId { get; init; }
    public Guid HandlingUnitId { get; init; }
    public string WarehouseId { get; init; } = string.Empty;
    public string SKU { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string FromLocation { get; init; } = string.Empty;
    public Guid OperatorId { get; init; }
}
