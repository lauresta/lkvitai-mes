using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Application.Queries;

/// <summary>
/// Query to get available stock for a specific (warehouseId, location, SKU).
/// Used by AllocationSaga and UI to determine what is available right now.
/// </summary>
public record GetAvailableStockQuery : ICommand<AvailableStockDto?>
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    public string WarehouseId { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string SKU { get; init; } = string.Empty;
}

/// <summary>
/// DTO for available stock at a specific (warehouseId, location, SKU).
/// </summary>
public record AvailableStockDto
{
    public string WarehouseId { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string SKU { get; init; } = string.Empty;

    /// <summary>Physical on-hand quantity.</summary>
    public decimal OnHandQty { get; init; }

    /// <summary>Total HARD-locked quantity.</summary>
    public decimal HardLockedQty { get; init; }

    /// <summary>Available quantity = max(0, OnHandQty - HardLockedQty).</summary>
    public decimal AvailableQty { get; init; }

    public DateTime LastUpdated { get; init; }
}
