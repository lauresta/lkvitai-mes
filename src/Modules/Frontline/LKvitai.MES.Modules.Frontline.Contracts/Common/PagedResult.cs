namespace LKvitai.MES.Modules.Frontline.Contracts.Common;

/// <summary>
/// Generic page envelope returned by Frontline list queries. Items, total count
/// and the echoed page parameters so callers can render pagination without
/// round-trips. Mirror of <c>Sales.Contracts.Common.PagedResult&lt;T&gt;</c> —
/// kept module-local until shared web contracts get extracted.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize);
