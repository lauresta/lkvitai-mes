using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Pricing;

public sealed class ItemPriceSnapshotService(WarehouseDbContext db) : IItemPriceSnapshotService
{
    public async Task<IReadOnlyList<ItemPriceSnapshot>> GetAsync(IReadOnlyCollection<int> itemIds,
        IReadOnlyCollection<string> skus, CancellationToken ct)
    {
        var ids = itemIds.Distinct().ToArray();
        var codes = skus.Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant()).Distinct().ToArray();
        if (ids.Length == 0 && codes.Length == 0) return [];

        var items = await db.Items.AsNoTracking()
            .Where(x => ids.Contains(x.Id) || codes.Contains(x.InternalSKU.ToLower()))
            .Select(x => new { x.Id, x.InternalSKU, x.PurchasePrice, x.BasePrice })
            .ToListAsync(ct);
        var matchedIds = items.Select(x => x.Id).ToArray();
        var values = await db.OnHandValues.AsNoTracking()
            .Where(x => matchedIds.Contains(x.ItemId))
            .Select(x => new { x.ItemId, x.UnitCost, x.LastUpdated })
            .ToListAsync(ct);
        var latest = values.GroupBy(x => x.ItemId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.LastUpdated).First().UnitCost);
        return items.Select(item =>
        {
            var resolved = ItemPriceResolver.Resolve(latest.GetValueOrDefault(item.Id), item.PurchasePrice);
            return new ItemPriceSnapshot(item.Id, item.InternalSKU, resolved.EffectiveCost,
                resolved.CostSource, item.PurchasePrice, item.BasePrice);
        }).ToList();
    }
}
