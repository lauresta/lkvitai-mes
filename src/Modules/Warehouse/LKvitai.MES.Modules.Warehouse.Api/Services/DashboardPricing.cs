using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Application.Services;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

// Shared by overview, category and warehouse totals. Each endpoint chooses its own
// grouping; this applies the same completeness and rounding rule to every group.
public static class DashboardPricing
{
    public static (decimal? Cost, decimal? Sale) Calculate(
        IEnumerable<(string Sku, decimal Quantity)> rows,
        IReadOnlyDictionary<string, ItemPriceSnapshot> prices)
    {
        decimal cost = 0m;
        decimal sale = 0m;
        var completeCost = true;
        var completeSale = true;

        foreach (var (sku, quantity) in rows.Where(x => x.Quantity > 0m))
        {
            if (!prices.TryGetValue(sku, out var price))
            {
                completeCost = false;
                completeSale = false;
                continue;
            }

            if (price.EffectiveCost.HasValue)
                cost += ItemPriceResolver.Value(quantity, price.EffectiveCost)!.Value;
            else
                completeCost = false;

            if (price.BasePrice.HasValue)
                sale += ItemPriceResolver.Value(quantity, price.BasePrice)!.Value;
            else
                completeSale = false;
        }

        return (completeCost ? cost : null, completeSale ? sale : null);
    }
}
