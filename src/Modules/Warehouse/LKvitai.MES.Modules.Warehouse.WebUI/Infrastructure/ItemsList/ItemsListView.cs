namespace LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure.ItemsList;

/// <summary>How the item list is shown: the table or the photo cards.</summary>
public enum ItemsViewMode
{
    Table,
    Cards
}

/// <summary>Pure helpers of the item list: view mode in the URL/localStorage, photo URLs, status dot.</summary>
public static class ItemsListView
{
    public const string QueryKey = "view";
    public const string StorageKey = "lk.wh.adminItems.view";

    private const string CardsToken = "cards";
    private const string TableToken = "table";
    private const string ThumbSize = "size=thumb";
    private const string OriginalSize = "size=original";

    /// <summary>Parses "cards" / "table" (any case); null for anything else, so callers can fall back.</summary>
    public static ItemsViewMode? Parse(string? value)
    {
        var token = value?.Trim();
        if (string.Equals(token, CardsToken, StringComparison.OrdinalIgnoreCase))
        {
            return ItemsViewMode.Cards;
        }

        return string.Equals(token, TableToken, StringComparison.OrdinalIgnoreCase) ? ItemsViewMode.Table : null;
    }

    /// <summary>The table is the default: it has no query parameter ("?view=cards" only for cards).</summary>
    public static string? ToQuery(ItemsViewMode mode) => mode == ItemsViewMode.Cards ? CardsToken : null;

    public static string ToStorage(ItemsViewMode mode) => mode == ItemsViewMode.Cards ? CardsToken : TableToken;

    public static ItemsViewMode FromQuery(string? value) => Parse(value) ?? ItemsViewMode.Table;

    /// <summary>The remembered mode applies only when the URL does not say anything (a valid ?view= wins).</summary>
    public static ItemsViewMode? RememberedFor(string? queryValue, string? storedValue)
        => Parse(queryValue) is null ? Parse(storedValue) : null;

    /// <summary>
    /// Full-size photo URL of a thumbnail URL. Keeps the URL relative (the base href routes it through the WebUI
    /// photo proxy); only the size parameter changes.
    /// </summary>
    public static string OriginalUrl(string? thumbUrl)
        => string.IsNullOrWhiteSpace(thumbUrl)
            ? string.Empty
            : thumbUrl.Replace(ThumbSize, OriginalSize, StringComparison.Ordinal);

    public static string StatusDotClass(string? status) => status switch
    {
        "Active" => "items-card__dot items-card__dot--ok",
        "Obsolete" => "items-card__dot items-card__dot--warn",
        _ => "items-card__dot items-card__dot--slate"
    };
}
