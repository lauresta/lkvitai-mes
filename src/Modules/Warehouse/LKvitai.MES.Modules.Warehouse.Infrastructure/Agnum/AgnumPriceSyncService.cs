using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Integration.Agnum;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum;

public sealed class AgnumPriceSyncService
{
    private readonly WarehouseDbContext _db;
    private readonly IAgnumApiClientFactory _clients;

    public AgnumPriceSyncService(WarehouseDbContext db, IAgnumApiClientFactory clients)
    {
        _db = db;
        _clients = clients;
    }

    public async Task<int> SyncAsync(int sndId, CancellationToken ct = default)
    {
        var prices = await _clients.GetForSndId(sndId).GetPricesAsync(ct);
        var links = await _db.AgnumProductLinks.AsNoTracking()
            .Where(x => x.SndId == sndId)
            .Select(x => new { x.ItemId, x.AgnumCode })
            .ToListAsync(ct);
        var linkedPrices = links
            .Where(x => prices.ContainsKey(x.AgnumCode))
            .GroupBy(x => x.ItemId)
            .Select(x => (x.Key, Price: prices[x.First().AgnumCode]))
            .ToList();
        if (linkedPrices.Count == 0) return 0;

        var ids = linkedPrices.Select(x => x.Key).ToArray();
        var items = await _db.Items.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var groups = await _db.PriceGroups.ToListAsync(ct);
        var group = groups.FirstOrDefault(x =>
            string.Equals(x.Code, "Didmeninė", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(x.Name, "Didmeninė", StringComparison.OrdinalIgnoreCase));
        if (group is null && linkedPrices.Any(x => x.Price.KN1.HasValue))
        {
            group = new PriceGroup { Code = "Didmeninė", Name = "Didmeninė" };
            _db.PriceGroups.Add(group);
            await _db.SaveChangesAsync(ct);
        }

        var overrides = group is null ? new Dictionary<int, ItemPriceOverride>() :
            await _db.ItemPriceOverrides.Where(x => x.PriceGroupId == group.Id && ids.Contains(x.ItemId))
                .ToDictionaryAsync(x => x.ItemId, ct);
        var now = DateTimeOffset.UtcNow;
        var changes = 0;
        foreach (var (itemId, price) in linkedPrices)
        {
            if (!items.TryGetValue(itemId, out var item)) continue;
            if (price.KN2 is { } basePrice && item.BasePrice != basePrice)
            {
                _db.ItemPriceHistories.Add(new ItemPriceHistory {
                    ItemId = itemId, PriceType = ItemPriceTypes.Base, OldAmount = item.BasePrice,
                    NewAmount = basePrice, ChangedBy = "agnum-sync", ChangedAt = now, Reason = "agnum-sync"
                });
                item.BasePrice = basePrice;
                changes++;
            }
            if (group is not null && price.KN1 is { } wholesalePrice)
            {
                overrides.TryGetValue(itemId, out var existing);
                if (existing is null || existing.Amount != wholesalePrice)
                {
                    _db.ItemPriceHistories.Add(new ItemPriceHistory {
                        ItemId = itemId, PriceType = ItemPriceTypes.GroupOverride, PriceGroupId = group.Id,
                        OldAmount = existing?.Amount, NewAmount = wholesalePrice,
                        ChangedBy = "agnum-sync", ChangedAt = now, Reason = "agnum-sync"
                    });
                    if (existing is null) _db.ItemPriceOverrides.Add(new ItemPriceOverride {
                        ItemId = itemId, PriceGroupId = group.Id, Amount = wholesalePrice
                    });
                    else existing.Amount = wholesalePrice;
                    changes++;
                }
            }
        }
        if (changes > 0) await _db.SaveChangesAsync(ct);
        return changes;
    }
}
