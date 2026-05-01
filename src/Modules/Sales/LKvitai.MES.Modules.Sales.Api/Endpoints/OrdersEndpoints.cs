using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Contracts.Common;
using LKvitai.MES.Modules.Sales.Contracts.Orders;
using Microsoft.AspNetCore.Http;

namespace LKvitai.MES.Modules.Sales.Api.Endpoints;

public static class OrdersEndpoints
{
    /// <summary>
    /// Maps the S-1 read-only Orders endpoints under <c>/api/sales</c>:
    /// <c>GET /api/sales/orders</c> and <c>GET /api/sales/orders/{number}</c>.
    /// Backed by <see cref="IOrdersQueryService"/> (in-memory stub in S-1, real
    /// SQL Server adapter in S-2). Authorization is inherited from the parent
    /// <c>/api/sales</c> group, which is wired with <c>RequireAuthorization()</c>
    /// in <c>Program.cs</c> using the shared Portal cookie scheme.
    /// </summary>
    public static RouteGroupBuilder MapOrdersEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/orders", GetOrdersAsync)
            .WithName("GetSalesOrders")
            .Produces<PagedResult<OrderSummaryDto>>(StatusCodes.Status200OK);

        group.MapGet("/orders/{number}", GetOrderDetailsAsync)
            .WithName("GetSalesOrderDetails")
            .Produces<OrderDetailsDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> GetOrdersAsync(
        [AsParameters] OrdersListRequest request,
        IOrdersQueryService orders,
        CancellationToken cancellationToken)
    {
        var query = request.ToQueryParams();
        var result = await orders.GetOrdersAsync(query, cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetOrderDetailsAsync(
        string number,
        IOrdersQueryService orders,
        CancellationToken cancellationToken)
    {
        var details = await orders.GetOrderDetailsAsync(number, cancellationToken).ConfigureAwait(false);
        return details is null ? Results.NotFound() : Results.Ok(details);
    }

    /// <summary>
    /// API-side binding shape for <c>GET /api/sales/orders</c>. All members are
    /// nullable so missing query string values bind to <c>null</c> instead of
    /// triggering a <c>BadHttpRequestException</c> from minimal-API binding.
    /// Mapped into the layer-neutral <see cref="OrdersQueryParams"/> with the
    /// Page / PageSize range guards applied here so the Application contract
    /// stays free of HTTP concerns.
    /// </summary>
    private sealed record OrdersListRequest(
        string? Search,
        string? Status,
        string? Store,
        string? Date,
        bool?   HasDebt,
        int?    Page,
        int?    PageSize)
    {
        public OrdersQueryParams ToQueryParams()
        {
            var page = Page is null or < 1 ? 1 : Page.Value;
            var size = PageSize is null or < 1 or > 500 ? 100 : PageSize.Value;
            return new OrdersQueryParams
            {
                Search   = Search,
                Status   = Status,
                Store    = Store,
                Date     = Date,
                HasDebt  = HasDebt ?? false,
                Page     = page,
                PageSize = size,
            };
        }
    }
}
