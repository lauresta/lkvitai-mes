namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public sealed record ItemPriceSnapshotDto(int ItemId, string Sku, decimal? EffectiveCost, string? CostSource,
    decimal? PurchasePrice, decimal? BasePrice);
