using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Contracts.Common;
using LKvitai.MES.Modules.Sales.Contracts.Orders;
using LKvitai.MES.Modules.Sales.Contracts.Samples;
namespace LKvitai.MES.Modules.Sales.Infrastructure.Stub;
public sealed class StubOrdersQueryService : IOrdersQueryService
{
    private readonly SalesSampleOrders samples = new();
    public Task<PagedResult<OrderSummaryDto>> GetOrdersAsync(OrdersQueryParams query,CancellationToken ct) => samples.GetOrdersAsync(query,ct);
    public Task<OrdersFilterOptionsDto> GetFilterOptionsAsync(CancellationToken ct) => samples.GetFilterOptionsAsync(ct);
    public Task<OrderDetailsDto?> GetOrderDetailsAsync(string number,CancellationToken ct) => samples.GetOrderDetailsAsync(number,ct);
}
