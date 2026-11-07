using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

public sealed class PriceMap
{
    public static PriceMap Empty { get; } = new([]);
    private readonly Dictionary<int, ItemPriceSnapshotDto> _byId;
    private readonly Dictionary<string, ItemPriceSnapshotDto> _bySku;

    public PriceMap(IEnumerable<ItemPriceSnapshotDto> prices)
    {
        var rows = prices.ToList();
        _byId = rows.ToDictionary(x => x.ItemId);
        _bySku = rows.GroupBy(x => x.Sku, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
    }

    public ItemPriceSnapshotDto? Get(int? itemId, string? sku)
    {
        if (itemId.HasValue && _byId.TryGetValue(itemId.Value, out var byId)) return byId;
        return !string.IsNullOrWhiteSpace(sku) && _bySku.TryGetValue(sku, out var bySku) ? bySku : null;
    }

    public decimal? Value(decimal qty, int? itemId, string? sku)
    {
        var cost = Get(itemId, sku)?.EffectiveCost;
        return cost.HasValue ? decimal.Round(qty * cost.Value, 2, MidpointRounding.AwayFromZero) : null;
    }
}
