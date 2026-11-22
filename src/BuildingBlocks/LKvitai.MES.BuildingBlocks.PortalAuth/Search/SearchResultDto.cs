namespace LKvitai.MES.BuildingBlocks.Search;

/// <summary>
/// One hit of the portal "Search everything" box (issue #375). Every module returns this same
/// shape from <c>GET /api/{module}/.../search?q=&amp;limit=</c>, and every module WebUI relays it
/// from <c>GET {PathBase}/shell/search</c>, so the shared top bar can group hits by
/// <see cref="Kind"/> without knowing the module.
/// </summary>
/// <remarks>
/// The contract lives in the PortalAuth building block only because that is the one block every
/// module API and every module WebUI already references (no new project in the Docker restore lists).
/// </remarks>
/// <param name="Kind">One of <see cref="SearchKinds"/>; the shell groups and labels by it.</param>
/// <param name="Title">Main line, e.g. an order number, an item name or a menu label.</param>
/// <param name="Subtitle">Optional second line with the same data the module's own list shows.</param>
/// <param name="Url">
/// From a module API: path relative to that module's WebUI root (<c>orders/AB-1-C</c>).
/// From a module WebUI: absolute path on the portal host (<c>/sales/orders/AB-1-C</c>).
/// </param>
/// <param name="Score">Optional relevance, 0..1, higher first inside a kind.</param>
public sealed record SearchResultDto(
    string Kind,
    string Title,
    string? Subtitle,
    string Url,
    double? Score = null);

/// <summary>Kinds the first version of the universal search returns.</summary>
public static class SearchKinds
{
    public const string Order = "order";
    public const string Customer = "customer";
    public const string Item = "item";
    public const string Supplier = "supplier";
    public const string Screen = "screen";
}

/// <summary>Shared request rules: minimum query length and the per-kind limit.</summary>
public static class SearchRequest
{
    public const int MinQueryLength = 2;
    public const int MaxQueryLength = 100;
    public const int DefaultLimit = 6;
    public const int MinLimit = 1;
    public const int MaxLimit = 8;

    /// <summary>Header a module WebUI sets when its screens are returned but its data search failed.</summary>
    public const string PartialHeader = "X-Shell-Search-Partial";

    /// <summary>Trims the query; <c>null</c> when it is too short (or absurdly long) to search.</summary>
    public static string? NormalizeQuery(string? q)
    {
        var trimmed = q?.Trim();
        return trimmed is null || trimmed.Length < MinQueryLength || trimmed.Length > MaxQueryLength ? null : trimmed;
    }

    public static int ClampLimit(int? limit) => Math.Clamp(limit ?? DefaultLimit, MinLimit, MaxLimit);

    /// <summary>
    /// Turns a module-relative url from the API into an absolute path under the WebUI path base.
    /// Anything that is not a plain relative path is dropped by returning <c>null</c>.
    /// </summary>
    public static string? ToAbsolutePath(string? pathBase, string? relativeUrl)
    {
        if (relativeUrl is null) return null;
        var url = relativeUrl.Trim();
        if (url.Contains("://", StringComparison.Ordinal) || url.StartsWith("//", StringComparison.Ordinal)
            || url.Contains('\\') || url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var prefix = string.IsNullOrWhiteSpace(pathBase) ? string.Empty : "/" + pathBase.Trim('/');
        return prefix + "/" + url.TrimStart('/');
    }
}
