namespace LKvitai.MES.Contracts.ReadModels;

/// <summary>
/// Read model for available stock per (warehouseId, location, SKU).
/// </summary>
public class AvailableStockView
{
    /// <summary>
    /// Composite key: "{warehouseId}:{location}:{sku}".
    /// </summary>
    public string Id { get; set; } = string.Empty;

    public string WarehouseId { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;

    // Optional denormalized fields for master-data UI/reporting.
    public int? ItemId { get; set; }
    public string? ItemName { get; set; }
    public string? LocationCode { get; set; }
    public string? LotNumber { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string? BaseUoM { get; set; }

    /// <summary>
    /// Physical on-hand quantity at this (location, SKU).
    /// </summary>
    public decimal OnHandQty { get; set; }

    /// <summary>
    /// HARD-locked quantity from reservation picking workflow.
    /// </summary>
    public decimal HardLockedQty { get; set; }

    /// <summary>
    /// Reserved quantity from master-data reservation contracts.
    /// </summary>
    public decimal ReservedQty { get; set; }

    /// <summary>
    /// Available quantity = max(0, OnHandQty - HardLockedQty - ReservedQty).
    /// </summary>
    public decimal AvailableQty { get; set; }

    public DateTime LastUpdated { get; set; }

    public void RecomputeAvailable()
    {
        AvailableQty = Math.Max(0m, OnHandQty - HardLockedQty - ReservedQty);
    }

    public static string ComputeId(string warehouseId, string location, string sku)
        => $"{warehouseId}:{location}:{sku}";
}
