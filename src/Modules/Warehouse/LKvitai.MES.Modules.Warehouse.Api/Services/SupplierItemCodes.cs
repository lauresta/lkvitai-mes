namespace LKvitai.MES.Modules.Warehouse.Api.Services;

/// <summary>
/// The supplier's own code of an item ("tiekėjo prekės kodas", <c>SupplierItemMapping.SupplierSKU</c>):
/// which of an item's codes to show next to it in a search result or on a purchase document.
/// </summary>
public static class SupplierItemCodes
{
    /// <summary>
    /// The code of the given supplier when it has one for the item; otherwise the code that matched the
    /// search text; otherwise nothing. An item with several suppliers never gets a guessed code.
    /// </summary>
    public static string? Pick(IEnumerable<(int SupplierId, string SupplierSku)> mappings, int? supplierId, string? search)
    {
        var list = mappings.Where(x => !string.IsNullOrWhiteSpace(x.SupplierSku)).ToList();
        if (list.Count == 0)
        {
            return null;
        }

        if (supplierId.HasValue)
        {
            var own = list.Where(x => x.SupplierId == supplierId.Value).Select(x => x.SupplierSku).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            if (own is not null)
            {
                return own;
            }
        }

        var text = search?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return list
            .Select(x => x.SupplierSku)
            .Where(x => x.Contains(text, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => string.Equals(x, text, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }
}
