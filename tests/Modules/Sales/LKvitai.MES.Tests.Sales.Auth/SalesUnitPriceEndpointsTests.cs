using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LKvitai.MES.Modules.Sales.Api.Endpoints;
using LKvitai.MES.Modules.Sales.Api.Security;
using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Application.Pricing;
using LKvitai.MES.Modules.Sales.Contracts.Quotes;
using LKvitai.MES.Modules.Sales.Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace LKvitai.MES.Tests.Sales.Auth;
public sealed class SalesUnitPriceEndpointsTests
{
    [Fact]
    public async Task Lookup_is_protected_read_only_and_retains_unit_price_precision()
    {
        var builder=WebApplication.CreateBuilder();builder.WebHost.UseTestServer();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddSalesProtectedAuthentication();
        var lookup=new Lookup();builder.Services.AddSingleton<ISalesUnitPriceLookup>(lookup);
        await using var app=builder.Build();app.UseAuthentication();app.UseAuthorization();
        app.MapGroup("/api/sales").RequireAuthorization(SalesCredentialProtector.Policy).MapOrderEntryPricingEndpoints();
        await app.StartAsync();var client=app.GetTestClient();
        const string url="/api/sales/order-entry/unit-price?typeId=-1025240863&componentId=2328&widthMm=800&heightMm=1500&quantity=1&orderDate=2026-10-31";
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync(url)).StatusCode);Assert.Equal(0,lookup.Calls);
        var codec=app.Services.GetRequiredService<SalesCredentialProtector>();
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",codec.CreateApi(codec.CreateSession(Guid.NewGuid(),"Employee",DateTimeOffset.UtcNow.AddHours(1))));
        var response=await client.GetAsync(url);Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var price=await response.Content.ReadFromJsonAsync<SalesUnitPriceResult>();
        Assert.Equal(20.8526m,price!.UnitPrice);Assert.Equal(SalesUnitPriceStatus.Found,price.Status);
        Assert.Equal(-1025240863,lookup.Last!.TypeId);Assert.Equal(new DateOnly(2026,10,31),lookup.Last.OrderDate);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync(url.Replace("widthMm=800","widthMm=0"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync(url.Replace("&quantity=1",string.Empty))).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed,(await client.PostAsync(url,null)).StatusCode);
    }
    private sealed class Lookup : ISalesUnitPriceLookup
    {
        public int Calls;public SalesUnitPriceQuery? Last;
        public Task<SalesUnitPriceResult> GetUnitPriceAsync(SalesUnitPriceQuery query,CancellationToken ct)
        {
            Calls++;SalesUnitPricePolicy.Validate(query);Last=query;
            return Task.FromResult(SalesUnitPricePolicy.Resolve(true,true,1,1,0,20.8526m,20.8526m));
        }
    }
}
