using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using LKvitai.MES.Modules.Sales.Contracts.Orders;
using LKvitai.MES.Modules.Sales.Contracts.Quotes;
using LKvitai.MES.Modules.Sales.Infrastructure.Sql;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LKvitai.MES.Tests.Sales.Auth;

/// <summary>Every live adapter must resolve the actor before any physical SQL
/// access. A missing mapping may not fall through to the shared service login.</summary>
public sealed class LegacySqlAdapterIdentityBoundaryTests
{
    [Theory]
    [InlineData("orders.list")]
    [InlineData("orders.filters")]
    [InlineData("orders.detail")]
    [InlineData("catalog")]
    [InlineData("references.series")]
    [InlineData("references.categories")]
    [InlineData("references.types")]
    [InlineData("references.type")]
    [InlineData("product.preview")]
    [InlineData("unit.price")]
    [InlineData("address")]
    [InlineData("editor")]
    [InlineData("receipt")]
    [InlineData("payment.references")]
    [InlineData("payment.bank")]
    [InlineData("order.create")]
    public async Task Missing_actor_mapping_fails_closed_before_database_access(string operation)
    {
        var options=new SalesSqlOptions{ConnectionString="Server=must-not-connect.invalid;Database=none"};
        var factory=new RejectingLegacySqlConnectionFactory();
        var query=new SqlOrdersQueryService(options,NullLogger<SqlOrdersQueryService>.Instance,factory);
        var references=new SqlSalesOrderEntryReferences(options,factory);
        var editor=new SqlSalesOrderEntryService(options,new SalesOrderEntryOptions
        {Enabled=true,ExpectedDatabaseGuid=Guid.NewGuid(),AllowedSeriesIds=[1],PaymentsEnabled=true},factory);
        var actor=Guid.NewGuid();
        Func<Task> run=operation switch
        {
            "orders.list"=>()=>query.GetOrdersAsync(new OrdersQueryParams(),default),
            "orders.filters"=>()=>query.GetFilterOptionsAsync(default),
            "orders.detail"=>()=>query.GetOrderDetailsAsync("VV-123-26",default),
            "catalog"=>()=>new SqlLiveOrderEntryCatalog(options,factory).GetAsync(default),
            "references.series"=>()=>references.GetSeriesAsync(null,1,25,false,default),
            "references.categories"=>()=>references.GetCategoriesAsync(false,default),
            "references.types"=>()=>references.GetTypesAsync(null,null,1,25,false,default),
            "references.type"=>()=>references.GetTypeDefinitionAsync(1,default),
            "product.preview"=>()=>new SqlLegacyProductPreview(options,factory).CalculateAsync(new(1,800,1500,1,null,null,new string?[12],OrderId:1),default),
            "unit.price"=>()=>new SqlSalesUnitPriceLookup(options,factory).GetUnitPriceAsync(new SalesUnitPriceQuery(1,1,800,1500,1,new DateOnly(2026,11,1)),default),
            "address"=>()=>new SqlOrderEntryAddressLookup(options,factory).GetAsync(null,"test",null,default),
            "editor"=>()=>editor.GetAsync(1,default),
            "receipt"=>()=>editor.GetOutcomeAsync(actor,Guid.NewGuid(),default),
            "payment.references"=>()=>editor.GetPaymentReferencesAsync(1,default),
            "payment.bank"=>()=>editor.SearchPaymentBankTransfersAsync(1,null,1,25,null,default),
            "order.create"=>()=>editor.CreateAsync(actor,new(Guid.NewGuid(),new(1,"VV-123-26",new(2026,11,1),new(2026,11,15),1,1,null,null)),default),
            _=>throw new ArgumentOutOfRangeException(nameof(operation))
        };
        var error=await Assert.ThrowsAsync<OrderEntryException>(run);
        Assert.Equal("LEGACY_MAPPING_REQUIRED",error.Code);
        Assert.Equal(403,error.Status);
        Assert.Equal(1,factory.OpenAttempts);
    }
}
