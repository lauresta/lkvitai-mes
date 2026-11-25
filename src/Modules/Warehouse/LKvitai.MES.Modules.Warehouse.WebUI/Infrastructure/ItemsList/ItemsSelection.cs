using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure.ItemsList;

/// <summary>
/// Selection of the item list shared by the table and the cards. Items are matched by Id: every page load creates
/// new instances, and the DTO record compares its tag list by reference.
/// </summary>
public static class ItemsSelection
{
    public static bool Contains(IEnumerable<AdminItemDto> selected, int itemId)
        => selected.Any(x => x.Id == itemId);

    /// <summary>Adds the item when it is not selected, removes it when it is.</summary>
    public static HashSet<AdminItemDto> Toggle(IEnumerable<AdminItemDto> selected, AdminItemDto item)
    {
        var current = selected.ToList();
        var result = current.Where(x => x.Id != item.Id).ToHashSet();
        if (result.Count == current.Count)
        {
            result.Add(item);
        }

        return result;
    }

    /// <summary>Swaps selected items for the freshly loaded instances with the same Id, so the grid shows them ticked.</summary>
    public static HashSet<AdminItemDto> Reconcile(IEnumerable<AdminItemDto> selected, IEnumerable<AdminItemDto> loaded)
    {
        var byId = loaded.GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First());
        return selected.Select(x => byId.TryGetValue(x.Id, out var fresh) ? fresh : x).ToHashSet();
    }
}
