namespace LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure.ItemsList;

/// <summary>
/// View mode, page and page size of the item list. Switching the view changes only the mode: page and page size
/// stay, so both views show the same slice.
/// </summary>
public sealed class ItemsListState
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 500;

    public ItemsViewMode ViewMode { get; private set; } = ItemsViewMode.Table;

    public int Page { get; private set; } = 1;

    public int PageSize { get; private set; } = DefaultPageSize;

    /// <summary>Sets the mode; returns true when it changed.</summary>
    public bool SwitchView(ItemsViewMode mode)
    {
        if (mode == ViewMode)
        {
            return false;
        }

        ViewMode = mode;
        return true;
    }

    public void SetPage(int page) => Page = Math.Max(1, page);

    public void ResetPage() => Page = 1;

    /// <summary>A new page size starts again from the first page.</summary>
    public void SetPageSize(int pageSize)
    {
        PageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        Page = 1;
    }

    /// <summary>Page size taken from the data grid, which may report 0 before its first load.</summary>
    public void AdoptGridPaging(int zeroBasedPage, int gridPageSize)
    {
        Page = Math.Max(1, zeroBasedPage + 1);
        if (gridPageSize > 0)
        {
            PageSize = gridPageSize;
        }
    }

    public static int PageCount(int totalCount, int pageSize)
        => pageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
}
