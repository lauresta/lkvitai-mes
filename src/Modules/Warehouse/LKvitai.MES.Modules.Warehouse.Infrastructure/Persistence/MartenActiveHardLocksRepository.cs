using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Contracts.ReadModels;
using Marten;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;

/// <summary>
/// Marten implementation of <see cref="IActiveHardLocksRepository"/>.
/// Queries the ActiveHardLockView Marten documents.
/// [MITIGATION R-4] Efficient HARD lock conflict detection.
/// </summary>
public class MartenActiveHardLocksRepository : IActiveHardLocksRepository
{
    private readonly IDocumentStore _store;

    public MartenActiveHardLocksRepository(IDocumentStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task<decimal> SumHardLockedQtyAsync(
        string warehouseId, string location, string sku, CancellationToken ct)
    {
        await using var session = _store.QuerySession();

        var locks = await session.Query<ActiveHardLockView>()
            .Where(x => x.Location == location && x.SKU == sku)
            .ToListAsync(ct);

        return locks.Sum(x => x.HardLockedQty);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActiveHardLockDto>> GetAllActiveLocksAsync(CancellationToken ct)
    {
        await using var session = _store.QuerySession();

        var locks = await session.Query<ActiveHardLockView>()
            .ToListAsync(ct);

        return locks.Select(l => new ActiveHardLockDto
        {
            ReservationId = l.ReservationId,
            WarehouseId = l.WarehouseId,
            Location = l.Location,
            SKU = l.SKU,
            HardLockedQty = l.HardLockedQty
        }).ToList();
    }
}
