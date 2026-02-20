using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Application.Queries;
using LKvitai.MES.Contracts.ReadModels;
using Marten;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;

/// <summary>
/// Marten implementation of <see cref="IAvailableStockRepository"/>.
/// Queries the AvailableStockView documents produced by the AvailableStockProjection.
/// </summary>
public class MartenAvailableStockRepository : IAvailableStockRepository
{
    private readonly IDocumentStore _store;

    public MartenAvailableStockRepository(IDocumentStore store)
    {
        _store = store;
    }

    public async Task<AvailableStockDto?> GetAvailableStockAsync(
        string warehouseId,
        string location,
        string sku,
        CancellationToken cancellationToken = default)
    {
        await using var session = _store.QuerySession();

        var key = AvailableStockView.ComputeId(warehouseId, location, sku);
        var view = await session.LoadAsync<AvailableStockView>(key, cancellationToken);

        return view == null ? null : ToDto(view);
    }

    public async Task<IReadOnlyList<AvailableStockDto>> GetAvailableStockForLocationAsync(
        string warehouseId,
        string location,
        CancellationToken cancellationToken = default)
    {
        await using var session = _store.QuerySession();

        var views = await session.Query<AvailableStockView>()
            .Where(v => v.WarehouseId == warehouseId && v.Location == location)
            .ToListAsync(cancellationToken);

        return views.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<AvailableStockDto>> GetAvailableStockForSkuAsync(
        string warehouseId,
        string sku,
        CancellationToken cancellationToken = default)
    {
        await using var session = _store.QuerySession();

        var views = await session.Query<AvailableStockView>()
            .Where(v => v.WarehouseId == warehouseId && v.SKU == sku)
            .ToListAsync(cancellationToken);

        return views.Select(ToDto).ToList();
    }

    private static AvailableStockDto ToDto(AvailableStockView view) => new()
    {
        WarehouseId = view.WarehouseId,
        Location = view.Location,
        SKU = view.SKU,
        OnHandQty = view.OnHandQty,
        HardLockedQty = view.HardLockedQty,
        AvailableQty = view.AvailableQty,
        LastUpdated = view.LastUpdated
    };
}
