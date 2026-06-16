namespace LKvitai.MES.Modules.Shopfloor.Contracts.Legacy;

/// <summary>
/// Filter + paging parameters for the legacy product types list endpoint.
/// <see cref="Removed"/> defaults to <c>false</c> so removed rows are hidden
/// unless explicitly requested.
/// </summary>
public sealed record LegacyProductTypesQuery
{
    public string? Search { get; init; }
    public bool? Mapped { get; init; }
    public bool Removed { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 100;
}
