using System.Diagnostics;
using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Contracts.Common;
using LKvitai.MES.Modules.Sales.Contracts.Orders;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

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

        // Filter options for the toolbar Status / Store dropdowns. The WebUI
        // uses these instead of hardcoded city/status names so every option
        // shown actually filters at least one row in the current data.
        group.MapGet("/orders/filters", GetOrdersFilterOptionsAsync)
            .WithName("GetSalesOrdersFilterOptions")
            .Produces<OrdersFilterOptionsDto>(StatusCodes.Status200OK);

        return group;
    }

    private static async Task<IResult> GetOrdersAsync(
        [AsParameters] OrdersListRequest request,
        IOrdersQueryService orders,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var query = request.ToQueryParams();
        var result = await orders.GetOrdersAsync(query, cancellationToken).ConfigureAwait(false);
        sw.Stop();

        loggerFactory.CreateLogger("LKvitai.MES.Modules.Sales.Api.Endpoints.Orders").LogInformation(
            "[SalesPerf] api GET /api/sales/orders page={Page} pageSize={PageSize} returned={Returned} total={Total} " +
            "search={HasSearch} status={HasStatus} store={HasStore} hasDebt={HasDebt} elapsedMs={ElapsedMs}",
            result.Page, result.PageSize, result.Items.Count, result.Total,
            !string.IsNullOrWhiteSpace(query.Search), !string.IsNullOrWhiteSpace(query.Status),
            !string.IsNullOrWhiteSpace(query.Store), query.HasDebt, sw.ElapsedMilliseconds);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetOrderDetailsAsync(
        string number,
        IOrdersQueryService orders,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var details = await orders.GetOrderDetailsAsync(number, cancellationToken).ConfigureAwait(false);
        sw.Stop();

        loggerFactory.CreateLogger("LKvitai.MES.Modules.Sales.Api.Endpoints.Orders").LogInformation(
            "[SalesPerf] api GET /api/sales/orders/{{number}} number={Number} found={Found} elapsedMs={ElapsedMs}",
            number, details is not null, sw.ElapsedMilliseconds);

        return details is null ? Results.NotFound() : Results.Ok(details);
    }

    private static async Task<IResult> GetOrdersFilterOptionsAsync(
        IOrdersQueryService orders,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var options = await orders.GetFilterOptionsAsync(cancellationToken).ConfigureAwait(false);
        sw.Stop();

        loggerFactory.CreateLogger("LKvitai.MES.Modules.Sales.Api.Endpoints.Orders").LogInformation(
            "[SalesPerf] api GET /api/sales/orders/filters statuses={StatusCount} stores={StoreCount} elapsedMs={ElapsedMs}",
            options.Statuses.Count, options.Stores.Count, sw.ElapsedMilliseconds);

        return Results.Ok(options);
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
