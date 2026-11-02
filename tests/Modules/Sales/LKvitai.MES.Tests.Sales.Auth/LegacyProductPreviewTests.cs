using LKvitai.MES.Modules.Sales.Application.OrderEntry;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using LKvitai.MES.Modules.Sales.Infrastructure.Sql;
using Xunit;
namespace LKvitai.MES.Tests.Sales.Auth;
public sealed class LegacyProductPreviewTests
{
    [Fact]
    public async Task Invalid_parameter_shape_is_a_validation_error_before_database_access()
    {
        var service=new SqlLegacyProductPreview(new SalesSqlOptions { ConnectionString="invalid" },new RejectingLegacySqlConnectionFactory());
        var request=new LegacyProductPreviewRequest(1,800,1500,1,null,null,null!);
        var exception=await Assert.ThrowsAsync<LegacyProductRuleException>(()=>service.CalculateAsync(request,default));
        Assert.Equal("PPP_SHAPE",exception.Code);
    }
    [Fact]
    public async Task Product_without_order_is_rejected_before_a_database_connection()
    {
        var service=new SqlLegacyProductPreview(new SalesSqlOptions { ConnectionString="invalid" },new RejectingLegacySqlConnectionFactory());
        var request=new LegacyProductPreviewRequest(1,800,1500,1,null,null,new string?[12],ProductId:123);
        var exception=await Assert.ThrowsAsync<LegacyProductRuleException>(()=>service.CalculateAsync(request,default));
        Assert.Equal("PRODUCT_ORDER_REQUIRED",exception.Code);
    }
}
