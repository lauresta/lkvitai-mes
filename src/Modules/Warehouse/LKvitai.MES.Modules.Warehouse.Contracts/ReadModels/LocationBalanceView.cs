namespace LKvitai.MES.Contracts.ReadModels;

/// <summary>
/// Read model for location balances (per warehouseId, location, SKU).
/// Updated by LocationBalanceProjection (ASYNC) from StockMovedEvent.
/// 
/// Lives in Contracts so that both Projections (writer) and Infrastructure (reader)
/// can reference it without creating a circular dependency.
/// </summary>
public class LocationBalanceView
{
    /// <summary>
    /// Composite key: "{warehouseId}:{location}:{sku}"
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    public string WarehouseId { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public DateTime LastUpdated { get; set; }
}
