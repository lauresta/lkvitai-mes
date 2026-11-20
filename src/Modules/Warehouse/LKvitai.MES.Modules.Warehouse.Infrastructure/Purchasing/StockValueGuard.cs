using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Purchasing;

/// <summary>
/// Blueprint 4.5: the stock account profile is fixed in every value event, so the category of an item (and anything
/// that changes which profile it resolves to) cannot change while the item holds stock value. Reads the OnHandValue
/// read model, which is built from the W1 value ledger.
/// </summary>
public static class StockValueGuard
{
    public const string Message = "The category cannot be changed while the stock value is not zero.";

    public static Task<bool> ItemHasStockValueAsync(WarehouseDbContext db, int itemId, CancellationToken ct)
        => db.OnHandValues.AsNoTracking().AnyAsync(x => x.ItemId == itemId && (x.TotalValue != 0m || x.Qty != 0m), ct);

    /// <summary>True when an item of the category or of any descendant category holds stock value.</summary>
    public static async Task<bool> CategoryTreeHasStockValueAsync(WarehouseDbContext db, int categoryId, CancellationToken ct)
    {
        var parents = await db.ItemCategories.AsNoTracking()
            .Select(c => new { c.Id, c.ParentCategoryId }).ToListAsync(ct);
        var ids = new HashSet<int> { categoryId };
        var queue = new Queue<int>(ids);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var child in parents.Where(c => c.ParentCategoryId == current && ids.Add(c.Id)))
            {
                queue.Enqueue(child.Id);
            }
        }

        var itemIds = db.Items.AsNoTracking().Where(i => ids.Contains(i.CategoryId)).Select(i => i.Id);
        return await db.OnHandValues.AsNoTracking()
            .AnyAsync(x => itemIds.Contains(x.ItemId) && (x.TotalValue != 0m || x.Qty != 0m), ct);
    }
}
