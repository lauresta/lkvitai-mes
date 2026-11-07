namespace LKvitai.MES.Contracts.ReadModels;

public sealed record ItemPriceSnapshot(int ItemId, string Sku, decimal? EffectiveCost, string? CostSource,
    decimal? PurchasePrice, decimal? BasePrice);
