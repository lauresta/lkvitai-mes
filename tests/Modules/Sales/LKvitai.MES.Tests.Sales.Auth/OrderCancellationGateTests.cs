using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using LKvitai.MES.Modules.Sales.Infrastructure.Sql;
using Xunit;
namespace LKvitai.MES.Tests.Sales.Auth;
public sealed class OrderCancellationGateTests
{
    [Theory]
    [InlineData(123L)]
    [InlineData(2148197450L)]
    public async Task Owner_disabled_cancellation_blocks_old_and_bigint_ids_before_SQL(long orderId)
    {
        var service=new SqlSalesOrderEntryService(new SalesSqlOptions{ConnectionString="Server=must-not-connect;Database=none"},new SalesOrderEntryOptions{Enabled=true},new RejectingLegacySqlConnectionFactory());
        var error=await Assert.ThrowsAsync<OrderEntryException>(()=>service.CancelAsync(Guid.NewGuid(),orderId,new CancelOrderCommand(Guid.NewGuid(),"revision"),default));
        Assert.Equal(403,error.Status);Assert.Equal("CANCELLATION_DISABLED",error.Code);
    }
}
