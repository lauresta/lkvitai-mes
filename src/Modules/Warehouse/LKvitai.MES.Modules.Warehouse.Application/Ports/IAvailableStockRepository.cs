using LKvitai.MES.Modules.Warehouse.Application.Queries;

namespace LKvitai.MES.Modules.Warehouse.Application.Ports;

/// <summary>
/// Repository interface for querying the AvailableStock projection.
/// Application defines this interface; Infrastructure provides the Marten implementation.
/// Used by AllocationSaga to determine "what is available right now" for a (warehouseId, location, sku).
/// </summary>
public interface IAvailableStockRepository
{
    /// <summary>
    /// Gets available stock for a specific (warehouseId, location, SKU).
    /// Returns null if no stock record exists.
    /// </summary>
    Task<AvailableStockDto?> GetAvailableStockAsync(
        string warehouseId,
        string location,
        string sku,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all available stock rows for a given location across all SKUs.
    /// </summary>
    Task<IReadOnlyList<AvailableStockDto>> GetAvailableStockForLocationAsync(
        string warehouseId,
        string location,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all available stock rows for a given SKU across all locations in a warehouse.
    /// Useful for allocation: "find any location with available stock for this SKU."
    /// </summary>
    Task<IReadOnlyList<AvailableStockDto>> GetAvailableStockForSkuAsync(
        string warehouseId,
        string sku,
        CancellationToken cancellationToken = default);
}
