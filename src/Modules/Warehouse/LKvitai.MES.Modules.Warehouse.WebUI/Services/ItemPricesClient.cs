using System.Net.Http.Json;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

public sealed class ItemPricesClient(IHttpClientFactory factory, ILogger<ItemPricesClient> logger)
{
    public async Task<PriceMap> GetMapAsync(IEnumerable<(int? ItemId, string? Sku)> keys, CancellationToken ct = default)
    {
        var rows = keys.ToList();
        var itemIds = rows.Where(x => x.ItemId.HasValue).Select(x => x.ItemId!.Value).Distinct().ToArray();
        var skus = rows.Select(x => x.Sku).Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (itemIds.Length == 0 && skus.Length == 0) return PriceMap.Empty;
        try
        {
            var client = factory.CreateClient("WarehouseApi");
            using var response = await client.PostAsJsonAsync("/api/warehouse/v1/item-price-snapshots",
                new { ItemIds = itemIds, Skus = skus }, ct);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ItemPriceSnapshotDto[]>(cancellationToken: ct);
            return new PriceMap(result ?? []);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            logger.LogWarning(ex, "Item price snapshot unavailable; show stock without prices.");
            return PriceMap.Empty;
        }
    }
}
