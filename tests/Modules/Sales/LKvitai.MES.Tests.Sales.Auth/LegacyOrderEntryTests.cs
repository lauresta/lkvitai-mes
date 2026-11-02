using System.Text.Json;
using LKvitai.MES.Modules.Sales.Application.OrderEntry;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using LKvitai.MES.Modules.Sales.Infrastructure.Sql;
using Xunit;
namespace LKvitai.MES.Tests.Sales.Auth;
public sealed class LegacyOrderEntryTests
{
    [Theory]
    [InlineData("VV-123","VV","VV-123-26",123,"26")]
    [InlineData("VV-123-A","VV","VV-123-26A",123,"26A")]
    [InlineData(" vv-00123-26A ","VV","VV-123-26A",123,"26A")]
    [InlineData("RY-TEST-123","RY-TEST","RY-TEST-123-26",123,"26")]
    [InlineData("RY-TEST-123-26","RY-TEST","RY-TEST-123-26",123,"26")]
    public void Numbers_use_complete_live_series_prefix_and_canonical_current_group(string input,string series,string canonical,int sequence,string group)
    {
        var parsed=LegacyOrderNumber.Normalize(input,series,2026);
        Assert.Equal(canonical,parsed.Canonical);Assert.Equal(sequence,parsed.Sequence);Assert.Equal(group,parsed.Group);
        Assert.Equal(parsed,LegacyOrderNumber.Normalize(parsed.Canonical,series,2026));
    }
    [Theory]
    [InlineData("XX-123")][InlineData("RY-TEST-0")][InlineData("RY-TEST-2147483648")]
    [InlineData("RY-TEST-123-ABCD")][InlineData("RY-TEST-123' OR 1=1")]
    public void Invalid_numbers_are_not_accepted_as_SQL_or_guessed_series(string text)
        => Assert.Throws<OrderEntryException>(()=>LegacyOrderNumber.Normalize(text,"RY-TEST",2026));

    [Fact]
    public async Task Saving_defaults_off_before_attempting_any_SQL_connection()
    {
        var service=new SqlSalesOrderEntryService(new SalesSqlOptions{ConnectionString="Server=does-not-exist;Database=none"},new SalesOrderEntryOptions(),new RejectingLegacySqlConnectionFactory());
        var command=new CreateOrderCommand(Guid.NewGuid(),new OrderHeaderInput(2136330188,"RY-TEST-123",new DateOnly(2026,10,31),new DateOnly(2026,11,14),1,2,null,null));
        var failure=await Assert.ThrowsAsync<OrderEntryException>(()=>service.CreateAsync(Guid.NewGuid(),command,default));
        Assert.Equal("WRITES_DISABLED",failure.Code);Assert.Equal(403,failure.Status);
    }
    [Fact]
    public async Task Enabled_saving_still_requires_explicit_target_and_series_allowlist()
    {
        var service=new SqlSalesOrderEntryService(new SalesSqlOptions{ConnectionString="Server=does-not-exist;Database=none"},new SalesOrderEntryOptions{Enabled=true},new RejectingLegacySqlConnectionFactory());
        var command=new CreateOrderCommand(Guid.NewGuid(),new OrderHeaderInput(2136330188,"RY-TEST-123",new DateOnly(2026,10,31),new DateOnly(2026,11,14),1,2,null,null));
        var failure=await Assert.ThrowsAsync<OrderEntryException>(()=>service.CreateAsync(Guid.NewGuid(),command,default));
        Assert.Equal("WRITE_CONFIGURATION",failure.Code);
    }
    [Fact]
    public void Client_cannot_overpost_audit_or_header_state_fields()
    {
        var json="""{"CommandId":"11111111-1111-1111-1111-111111111111","Header":{"SeriesId":1,"Number":"X-1","Date":"2026-10-31","Due":"2026-11-14","CustomerId":1,"StoreId":1,"ManagerId":null,"ConsultantId":null,"StateId":2}}""";
        Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<CreateOrderCommand>(json));
    }
}
