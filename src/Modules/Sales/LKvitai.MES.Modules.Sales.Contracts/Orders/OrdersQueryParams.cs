namespace LKvitai.MES.Modules.Sales.Contracts.Orders;

/// <summary>
/// Query inputs for the orders list. Mirrors the S-0 toolbar: free-text search,
/// status / store filters, a date preset (S-1 keeps the legacy "30d" / "month"
/// / "ytd" preset selector — a real date range comes in S-2), the "has debt"
/// toggle and pagination.
/// </summary>
public sealed record OrdersQueryParams
{
    /// <summary>Free-text search across number, customer, address (and product text in S-2).</summary>
    public string? Search { get; init; }

    /// <summary>Localized status label filter (e.g. "Įvestas"). Empty = all statuses.</summary>
    public string? Status { get; init; }

    /// <summary>Store / branch filter (e.g. "Vilnius"). Empty = all stores.</summary>
    public string? Store { get; init; }

    /// <summary>
    /// Date preset selector — one of <c>30d</c>, <c>month</c>, <c>ytd</c>.
    /// S-1 uses presets only; an explicit date range arrives in S-2 (see handoff R-7).
    /// </summary>
    public string? Date { get; init; }

    /// <summary>When <c>true</c>, return only orders with outstanding debt.</summary>
    public bool HasDebt { get; init; }

    /// <summary>1-based page index. Defaults to 1.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Items per page. Defaults to 100 to match the S-0 dense table target.</summary>
    public int PageSize { get; init; } = 100;
}
