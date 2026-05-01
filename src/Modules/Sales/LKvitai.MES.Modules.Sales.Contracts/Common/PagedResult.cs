namespace LKvitai.MES.Modules.Sales.Contracts.Common;

/// <summary>
/// Generic page envelope returned by Sales list queries. Items, total count and the
/// echoed page parameters so callers can render pagination without round-trips.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize);
