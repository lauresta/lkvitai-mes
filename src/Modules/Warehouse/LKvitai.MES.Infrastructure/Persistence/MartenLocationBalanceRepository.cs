using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Application.Queries;
using LKvitai.MES.Contracts.ReadModels;
using Marten;

namespace LKvitai.MES.Infrastructure.Persistence;

/// <summary>
/// Marten implementation of LocationBalance repository
/// Queries LocationBalanceView projection
/// </summary>
public class MartenLocationBalanceRepository : ILocationBalanceRepository
{
    private readonly IDocumentSession _session;
    
    public MartenLocationBalanceRepository(IDocumentSession session)
    {
        _session = session;
    }
    
    public async Task<LocationBalanceDto?> GetBalanceAsync(
        string warehouseId,
        string location,
        string sku,
        CancellationToken cancellationToken = default)
    {
        // Query LocationBalanceView by composite key
        var key = $"{warehouseId}:{location}:{sku}";
        var view = await _session.LoadAsync<LocationBalanceView>(key, cancellationToken);
        
        if (view == null)
            return null;
        
        return new LocationBalanceDto
        {
            WarehouseId = warehouseId,
            Location = view.Location,
            SKU = view.SKU,
            Quantity = view.Quantity,
            LastUpdated = view.LastUpdated
        };
    }
    
    public async Task<IReadOnlyList<LocationBalanceDto>> GetBalancesForLocationAsync(
        string warehouseId,
        string location,
        CancellationToken cancellationToken = default)
    {
        // Query all balances for a specific location
        // Marten will use identity prefix matching
        var views = await _session.Query<LocationBalanceView>()
            .Where(v => v.Location == location)
            .ToListAsync(cancellationToken);
        
        return views
            .Select(v => new LocationBalanceDto
            {
                WarehouseId = warehouseId,
                Location = v.Location,
                SKU = v.SKU,
                Quantity = v.Quantity,
                LastUpdated = v.LastUpdated
            })
            .ToList();
    }
}
