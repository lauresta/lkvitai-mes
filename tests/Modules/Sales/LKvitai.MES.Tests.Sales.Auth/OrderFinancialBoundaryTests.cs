using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using LKvitai.MES.Modules.Sales.Infrastructure.Sql;
using Xunit;
namespace LKvitai.MES.Tests.Sales.Auth;
public sealed class OrderFinancialBoundaryTests
{
    [Fact]
    public async Task Independent_financial_flags_default_off_without_a_database_connection()
    {
        var service=new SqlSalesOrderEntryService(new SalesSqlOptions{ConnectionString="Server=must-not-connect;Database=none"},new SalesOrderEntryOptions{Enabled=true},new RejectingLegacySqlConnectionFactory());
        var pay=new PostOrderPaymentCommand(Guid.NewGuid(),"revision",new OrderPaymentInput(new DateOnly(2026,10,31),0.01m,81423987,2138337252));
        var payment=await Assert.ThrowsAsync<OrderEntryException>(()=>service.PostPaymentAsync(Guid.NewGuid(),1,pay,default));
        Assert.Equal("PAYMENTS_DISABLED",payment.Code);Assert.Equal(403,payment.Status);
        var confirm=new ConfirmOrderProductsCommand(Guid.NewGuid(),"revision",new long[]{2152012295});
        var confirmation=await Assert.ThrowsAsync<OrderEntryException>(()=>service.ConfirmProductsAsync(Guid.NewGuid(),1,confirm,default));
        Assert.Equal("CONFIRMATION_DISABLED",confirmation.Code);Assert.Equal(403,confirmation.Status);
    }
    [Fact]
    public async Task A_financial_flag_does_not_override_the_global_write_gate()
    {
        var service=new SqlSalesOrderEntryService(new SalesSqlOptions{ConnectionString="Server=must-not-connect;Database=none"},new SalesOrderEntryOptions{PaymentsEnabled=true,ProductionConfirmationEnabled=true},new RejectingLegacySqlConnectionFactory());
        var payment=await Assert.ThrowsAsync<OrderEntryException>(()=>service.PostPaymentAsync(Guid.NewGuid(),1,new PostOrderPaymentCommand(Guid.NewGuid(),"revision",new OrderPaymentInput(new DateOnly(2026,10,31),0.01m,81423987,1)),default));
        var confirmation=await Assert.ThrowsAsync<OrderEntryException>(()=>service.ConfirmProductsAsync(Guid.NewGuid(),1,new ConfirmOrderProductsCommand(Guid.NewGuid(),"revision",new long[]{2152012295}),default));
        Assert.Equal("WRITES_DISABLED",payment.Code);Assert.Equal("WRITES_DISABLED",confirmation.Code);
    }
}
