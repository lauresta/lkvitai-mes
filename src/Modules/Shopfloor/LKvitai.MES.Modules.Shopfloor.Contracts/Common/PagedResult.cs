namespace LKvitai.MES.Modules.Shopfloor.Contracts.Common;

/// <summary>A single page of results plus the total row count.</summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Total,
    int Page,
    int PageSize);
