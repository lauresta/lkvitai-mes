namespace LKvitai.MES.Modules.Frontline.Contracts.Fabric;

/// <summary>
/// Query parameters for the desktop low-stock list. Mirrors the toolbar in
/// <c>FabricLowStock.razor</c>: search box + threshold + status + width +
/// supplier + paging.
/// </summary>
/// <remarks>
/// All members are nullable so missing query-string values bind to <c>null</c>
/// instead of triggering a <c>BadHttpRequestException</c> in minimal-API
/// binding. Range guards (Page ≥ 1, PageSize 1..500) live in the API layer's
/// request mapper, not here, so the application contract stays HTTP-free.
///
/// <see cref="ThresholdMeters"/> semantics:
/// <list type="bullet">
///   <item><c>null</c> — no threshold filter; show all low-stock rows.</item>
///   <item><c>0</c> — show only rows with <see cref="FabricAvailabilityStatus.None"/>
///   (matches the toolbar's "Out of stock only" option).</item>
///   <item><c>&gt; 0</c> — keep rows where <c>AvailableMeters ≤ value</c> OR
///   the row is already Discontinued / Out (so the highlight survives the
///   threshold cut-off).</item>
/// </list>
/// </remarks>
public sealed record FabricLowStockQueryParams
{
    public string? Search { get; init; }
    public int? ThresholdMeters { get; init; }
    public string? Status { get; init; }
    public int? WidthMm { get; init; }
    public string? Supplier { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
