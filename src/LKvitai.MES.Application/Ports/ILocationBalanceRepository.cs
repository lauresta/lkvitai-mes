using LKvitai.MES.Application.Queries;

namespace LKvitai.MES.Application.Ports;

/// <summary>
/// Repository interface for querying LocationBalance projection
/// Boundary: Application defines interface, Infrastructure implements
/// </summary>
public interface ILocationBalanceRepository
{
    /// <summary>
    /// Gets balance for specific (warehouseId, location, SKU)
    /// Returns null if no balance found
    /// </summary>
    Task<LocationBalanceDto?> GetBalanceAsync(
        string warehouseId,
        string location,
        string sku,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all balances for a specific location
    /// </summary>
    Task<IReadOnlyList<LocationBalanceDto>> GetBalancesForLocationAsync(
        string warehouseId,
        string location,
        CancellationToken cancellationToken = default);
}
