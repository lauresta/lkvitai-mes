using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Application.Commands;

/// <summary>
/// Command to record a stock movement in the StockLedger.
/// Handled with expected-version append (V-2) and bounded retries.
/// </summary>
public record RecordStockMovementCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    /// <summary>Warehouse identifier (maps to StockLedger stream via ADR-001).</summary>
    public string WarehouseId { get; init; } = string.Empty;

    public string SKU { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string FromLocation { get; init; } = string.Empty;
    public string ToLocation { get; init; } = string.Empty;
    public string MovementType { get; init; } = string.Empty;
    public Guid OperatorId { get; init; }
    public Guid? HandlingUnitId { get; init; }
    public string? Reason { get; init; }
}
