using LKvitai.MES.Modules.Sales.Contracts.Common;
using LKvitai.MES.Modules.Sales.Contracts.Orders;

namespace LKvitai.MES.Modules.Sales.Application.Ports;

/// <summary>
/// Read-side port for sales orders queries. Application owns the contract;
/// Infrastructure provides an implementation (stub in S-1, SQL Server adapter
/// over the legacy <c>weblb_*</c> stored procedures in S-2).
/// </summary>
public interface IOrdersQueryService
{
    /// <summary>
    /// Returns a page of order summaries matching the supplied filters. Implementations
    /// must echo the requested <see cref="OrdersQueryParams.Page"/> and
    /// <see cref="OrdersQueryParams.PageSize"/> on the result envelope so the UI can
    /// render pagination without a separate count call.
    /// </summary>
    Task<PagedResult<OrderSummaryDto>> GetOrdersAsync(
        OrdersQueryParams query,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the full details payload for a single order by its public number,
    /// or <c>null</c> when no such order exists.
    /// </summary>
    Task<OrderDetailsDto?> GetOrderDetailsAsync(
        string number,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns distinct values currently present in the data for the toolbar
    /// Status and Store dropdowns. The WebUI uses these to populate the
    /// dropdowns dynamically instead of hardcoding labels — guarantees that
    /// every option, when picked, actually filters at least one row.
    /// </summary>
    Task<OrdersFilterOptionsDto> GetFilterOptionsAsync(
        CancellationToken cancellationToken);
}
