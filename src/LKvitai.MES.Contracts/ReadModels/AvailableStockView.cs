namespace LKvitai.MES.Contracts.ReadModels;

/// <summary>
/// Read model for available stock per (warehouseId, location, SKU).
/// Combines on-hand quantity (from StockMoved events) with hard-locked
/// quantity (from PickingStarted / ReservationConsumed / ReservationCancelled events).
///
/// availableQty = max(0, onHandQty - hardLockedQty)
///
/// Lives in Contracts so that both Projections (writer) and Infrastructure (reader)
/// can reference it without creating a circular dependency.
///
/// SOFT allocations do NOT reduce available quantity (overbooking is allowed per Req 3.3).
/// Only HARD locks reduce availability.
/// </summary>
public class AvailableStockView
{
    /// <summary>
    /// Composite key: "{warehouseId}:{location}:{sku}"
    /// </summary>
    public string Id { get; set; } = string.Empty;

    public string WarehouseId { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;

    /// <summary>
    /// Physical on-hand quantity at this (location, SKU).
    /// Updated by StockMoved events (increase for TO, decrease for FROM).
    /// </summary>
    public decimal OnHandQty { get; set; }

    /// <summary>
    /// Total HARD-locked quantity at this (location, SKU).
    /// Increased by PickingStartedEvent, decreased by ReservationConsumed/Cancelled.
    /// </summary>
    public decimal HardLockedQty { get; set; }

    /// <summary>
    /// Available quantity = max(0, OnHandQty - HardLockedQty).
    /// Computed on every update.
    /// </summary>
    public decimal AvailableQty { get; set; }

    public DateTime LastUpdated { get; set; }

    /// <summary>
    /// Recomputes AvailableQty, ensuring it is never negative.
    /// </summary>
    public void RecomputeAvailable()
    {
        AvailableQty = Math.Max(0m, OnHandQty - HardLockedQty);
    }

    /// <summary>
    /// Computes the deterministic document Id for an available stock row.
    /// </summary>
    public static string ComputeId(string warehouseId, string location, string sku)
        => $"{warehouseId}:{location}:{sku}";
}
