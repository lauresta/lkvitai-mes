namespace LKvitai.MES.Modules.Warehouse.Application.Services;

public static class ItemPriceResolver
{
    public static (decimal? EffectiveCost, string? CostSource) Resolve(decimal? wacUnitCost, decimal? purchasePrice)
        => wacUnitCost is > 0m ? (wacUnitCost, "WAC")
            : purchasePrice is > 0m ? (purchasePrice, "Purchase")
            : (null, null);

    public static decimal? Value(decimal qty, decimal? cost)
        => cost.HasValue ? decimal.Round(qty * cost.Value, 2, MidpointRounding.AwayFromZero) : null;
}
