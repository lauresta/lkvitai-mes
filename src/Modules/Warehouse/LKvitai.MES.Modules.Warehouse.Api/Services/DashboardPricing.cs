using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Application.Services;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

// Shared by overview, category and warehouse totals. Each endpoint chooses its own
// grouping; this applies the same priced-row and rounding rule to every group.
public static class DashboardPricing
{
    public static (decimal? Cost, decimal? Sale, int MissingCost, int MissingSale) Calculate(
        IEnumerable<(string Sku, decimal Quantity)> rows,
        IReadOnlyDictionary<string, ItemPriceSnapshot> prices)
    {
        decimal cost = 0m;
        decimal sale = 0m;
        var missingCost = 0;
        var missingSale = 0;

        foreach (var (sku, quantity) in rows.Where(x => x.Quantity > 0m))
        {
            if (!prices.TryGetValue(sku, out var price))
            {
                missingCost++;
                missingSale++;
                continue;
            }

            if (price.EffectiveCost.HasValue)
                cost += ItemPriceResolver.Value(quantity, price.EffectiveCost)!.Value;
            else
                missingCost++;

            if (price.BasePrice.HasValue)
                sale += ItemPriceResolver.Value(quantity, price.BasePrice)!.Value;
            else
                missingSale++;
        }

        return (cost, sale, missingCost, missingSale);
    }
}
