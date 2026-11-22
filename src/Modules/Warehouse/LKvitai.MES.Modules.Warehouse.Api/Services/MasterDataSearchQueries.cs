using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

/// <summary>
/// The free-text predicates of the item and supplier lists, shared by the list endpoints and by the
/// portal search endpoint (issue #375) so both find exactly the same rows.
/// </summary>
public static class MasterDataSearchQueries
{
    /// <summary>
    /// Items by internal code, name, exact primary barcode, tag, or the supplier's own code of the item.
    /// <paramref name="normalized"/> is the trimmed, lower-cased search text.
    /// </summary>
    public static IQueryable<Item> WhereMatches(this IQueryable<Item> query, WarehouseDbContext dbContext, string normalized)
    {
        var tag = normalized.TrimStart('#');
        return query.Where(i =>
            i.InternalSKU.ToLower().Contains(normalized) ||
            i.Name.ToLower().Contains(normalized) ||
            (i.PrimaryBarcode != null && i.PrimaryBarcode.ToLower() == normalized) ||
            (i.Tags != null && i.Tags.ToLower().Contains(tag)) ||
            // The supplier's own code of the item, so a code read off a supplier invoice finds it.
            dbContext.SupplierItemMappings.Any(m => m.ItemId == i.Id && m.SupplierSKU.ToLower().Contains(normalized)));
    }

    /// <summary>Suppliers by code, names, company/VAT code, contact and place, as the supplier list searches.</summary>
    public static IQueryable<Supplier> WhereMatches(this IQueryable<Supplier> query, string normalized)
        => query.Where(x =>
            x.Code.ToLower().Contains(normalized) ||
            x.Name.ToLower().Contains(normalized) ||
            (x.ShortName != null && x.ShortName.ToLower().Contains(normalized)) ||
            (x.CompanyCode != null && x.CompanyCode.ToLower().Contains(normalized)) ||
            (x.VatCode != null && x.VatCode.ToLower().Contains(normalized)) ||
            (x.Email != null && x.Email.ToLower().Contains(normalized)) ||
            (x.Phone != null && x.Phone.ToLower().Contains(normalized)) ||
            (x.Country != null && x.Country.ToLower().Contains(normalized)) ||
            (x.City != null && x.City.ToLower().Contains(normalized)));

    /// <summary>Exact SKU matches first, then SKU prefix matches, then the rest by SKU.</summary>
    public static IOrderedQueryable<Item> OrderBySkuRelevance(this IQueryable<Item> query, string normalized)
        => query
            .OrderBy(i => i.InternalSKU.ToLower() == normalized ? 0 : i.InternalSKU.ToLower().StartsWith(normalized) ? 1 : 2)
            .ThenBy(i => i.InternalSKU);
}
