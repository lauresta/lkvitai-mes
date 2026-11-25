using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure.ItemCard;

/// <summary>
/// Full update requests built from the loaded item, so the header "Keisti" edits change one field and keep
/// every other field as it is (the same normalisation as <c>AdminItemEditorDialog</c>).
/// </summary>
public static class ItemCardRequests
{
    public static CreateOrUpdateItemRequest FromItem(ItemDetailsDto item)
    {
        var isService = string.Equals(item.ItemType, "Service", StringComparison.OrdinalIgnoreCase);
        var supplementaryUnit = Normalize(item.SupplementaryUnit);

        return new CreateOrUpdateItemRequest
        {
            InternalSKU = null,
            Name = item.Name.Trim(),
            Description = Normalize(item.Description),
            CategoryId = item.CategoryId,
            BaseUoM = item.BaseUoM.Trim(),
            Weight = item.Weight,
            Volume = item.Volume,
            BasePrice = item.BasePrice,
            PurchasePrice = item.PurchasePrice,
            MinQty = item.MinQty,
            RequiresLotTracking = item.RequiresLotTracking,
            RequiresQC = item.RequiresQC,
            Status = string.IsNullOrWhiteSpace(item.Status) ? "Active" : item.Status.Trim(),
            PrimaryBarcode = Normalize(item.PrimaryBarcode),
            ProductConfigId = Normalize(item.ProductConfigId),
            Tags = item.Tags.ToArray(),
            ItemType = string.IsNullOrWhiteSpace(item.ItemType) ? "Stock" : item.ItemType.Trim(),
            CostType = isService ? Normalize(item.CostType) : null,
            CommodityCode = Normalize(item.CommodityCode),
            NetWeightKg = item.NetWeightKg,
            OriginCountry = Normalize(item.OriginCountry)?.ToUpperInvariant(),
            SupplementaryUnit = supplementaryUnit,
            SupplementaryUnitFactor = supplementaryUnit is null ? null : item.SupplementaryUnitFactor
        };
    }

    public static CreateOrUpdateItemRequest WithBasePrice(ItemDetailsDto item, decimal? basePrice)
        => FromItem(item) with { BasePrice = basePrice };

    public static CreateOrUpdateItemRequest WithMinQty(ItemDetailsDto item, decimal? minQty)
        => FromItem(item) with { MinQty = minQty };

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
