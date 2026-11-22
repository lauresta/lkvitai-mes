using LKvitai.MES.BuildingBlocks.Search;
using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Contracts.Orders;

namespace LKvitai.MES.Modules.Sales.Api.Endpoints;

/// <summary>
/// Sales part of the portal "Search everything" box (issue #375):
/// <c>GET /api/sales/search?q=&amp;limit=</c> → <see cref="SearchResultDto"/>[] with kinds
/// <see cref="SearchKinds.Order"/> and <see cref="SearchKinds.Customer"/>.
/// </summary>
/// <remarks>
/// Orders are a thin projection of the orders list: <see cref="IOrdersQueryService.GetOrdersAsync"/>,
/// i.e. the same query, the same legacy login and the same <c>dbo.Loc_leidimas()</c> branch scope as
/// <c>GET /api/sales/orders</c>. Customers come from <see cref="ISalesCustomerSearch"/> (customers of
/// orders the caller may see); a host without it (the explicit stub data source) derives them from
/// the matched orders. Authorization is inherited from the parent <c>/api/sales</c> group exactly
/// like the list endpoint. Nothing is returned that the list does not already show.
/// </remarks>
public static class SearchEndpoints
{
    /// <summary>Orders read per search; orders matched by number are ranked first among these rows.</summary>
    public const int OrdersWindow = 40;

    public static RouteGroupBuilder MapSearchEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/search", async (string? q, int? limit, IOrdersQueryService orders, HttpContext http, CancellationToken ct) =>
            {
                http.Response.Headers.CacheControl = "no-store";
                var customers = http.RequestServices.GetService<ISalesCustomerSearch>();
                return Results.Ok(await SearchAsync(orders, q, limit, ct, customers).ConfigureAwait(false));
            })
            .WithName("SearchSales")
            .Produces<IReadOnlyList<SearchResultDto>>(StatusCodes.Status200OK);

        return group;
    }

    public static async Task<IReadOnlyList<SearchResultDto>> SearchAsync(
        IOrdersQueryService orders, string? q, int? limit, CancellationToken cancellationToken,
        ISalesCustomerSearch? customers = null)
    {
        ArgumentNullException.ThrowIfNull(orders);

        var query = SearchRequest.NormalizeQuery(q);
        if (query is null) return [];

        var perKind = SearchRequest.ClampLimit(limit);
        var page = await orders.GetOrdersAsync(
            new OrdersQueryParams { Search = query, Page = 1, PageSize = OrdersWindow },
            cancellationToken).ConfigureAwait(false);

        var customerHits = customers is null
            ? CustomerResults(page.Items, query, perKind)
            : CustomerResults(await customers.SearchAsync(query, perKind, cancellationToken).ConfigureAwait(false), query, perKind);

        return [.. OrderResults(page.Items, query, perKind), .. customerHits];
    }

    /// <summary>The list order (newest first) is kept; orders matched by their number go on top.</summary>
    private static IEnumerable<SearchResultDto> OrderResults(IReadOnlyList<OrderSummaryDto> rows, string query, int limit)
        => rows
            .Where(o => !string.IsNullOrWhiteSpace(o.Number))
            .Select((o, index) => (o, index, score: NumberScore(o.Number, query)))
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.index)
            .Take(limit)
            .Select(x => new SearchResultDto(
                SearchKinds.Order,
                x.o.Number,
                JoinNonEmpty(x.o.Customer, x.o.Status, x.o.Store),
                "orders/" + Uri.EscapeDataString(x.o.Number),
                x.score));

    /// <summary>
    /// Fallback without <see cref="ISalesCustomerSearch"/>: customers as they appear on the matched
    /// orders. Rows flagged <see cref="OrderSummaryDto.IsPrivacyRestricted"/> never produce a customer
    /// hit: the order row keeps what the list shows and nothing more is derived from it.
    /// </summary>
    private static IEnumerable<SearchResultDto> CustomerResults(IReadOnlyList<OrderSummaryDto> rows, string query, int limit)
        => CustomerResults(
            rows.Where(o => !o.IsPrivacyRestricted).Select(o => o.Customer)
                .Where(name => SearchText.Score(name, query) > 0).ToList(),
            query,
            limit);

    /// <summary>Customer names (already matched by the source) as hits that open the filtered orders list.</summary>
    private static IEnumerable<SearchResultDto> CustomerResults(IReadOnlyList<string> names, string query, int limit)
        => names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => (name, score: Math.Max(SearchText.Score(name, query), 0.4)))
            .OrderByDescending(x => x.score)
            .Take(limit)
            .Select(x => new SearchResultDto(
                SearchKinds.Customer,
                x.name,
                null,
                "?search=" + Uri.EscapeDataString(x.name),
                x.score));

    private static double NumberScore(string number, string query)
    {
        if (string.Equals(number, query, StringComparison.OrdinalIgnoreCase)) return 1;
        return number.Contains(query, StringComparison.OrdinalIgnoreCase) ? 0.8 : 0.5;
    }

    private static string? JoinNonEmpty(params string?[] parts)
    {
        var text = string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
        return text.Length == 0 ? null : text;
    }
}
