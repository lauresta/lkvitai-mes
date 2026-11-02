using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LKvitai.MES.Modules.Sales.Api.Endpoints;
using LKvitai.MES.Modules.Sales.Api.Security;
using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Contracts.Common;
using LKvitai.MES.Modules.Sales.Contracts.References;
using LKvitai.MES.Modules.Sales.Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LKvitai.MES.Tests.Sales.Auth;

public sealed class OrderEntryReferencesTests
{
    [Fact]
    public async Task References_require_trusted_credentials_and_preserve_signed_legacy_ids()
    {
        var builder=WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddSalesProtectedAuthentication();
        var source=new References();
        builder.Services.AddSingleton<ISalesOrderEntryReferences>(source);
        await using var app=builder.Build();
        app.UseAuthentication();app.UseAuthorization();
        app.MapGroup("/api/sales").RequireAuthorization(SalesCredentialProtector.Policy).MapOrderEntryReferenceEndpoints();
        await app.StartAsync();
        var client=app.GetTestClient();
        var url="/api/sales/order-entry/references/types/-1025240863";
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync(url)).StatusCode);
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer","forged|admin");
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync(url)).StatusCode);
        Assert.Equal(0,source.Calls);
        var protector=app.Services.GetRequiredService<SalesCredentialProtector>();
        var session=protector.CreateSession(Guid.NewGuid(),"Employee",DateTimeOffset.UtcNow.AddHours(1));
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",protector.CreateApi(session));
        var response=await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var type=await response.Content.ReadFromJsonAsync<ProductTypeDefinitionDto>();
        Assert.Equal(-1025240863,type!.Type.Id);
        Assert.Equal(12,type.Parameters.Count);
        Assert.Equal(ProductParameterStorageKind.Decimal,type.Parameters[11].StorageKind);
        Assert.Equal("any",type.Pricing.PriceEntryMode);
        Assert.Equal(-1789163110,type.Pricing.UnitId);
        Assert.Equal(0.25m,type.Pricing.MinimumWidthM);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/api/sales/order-entry/references/types/123")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync("/api/sales/order-entry/references/series?page=0")).StatusCode);
        var series=await client.GetFromJsonAsync<PagedResult<OrderSeriesReferenceDto>>("/api/sales/order-entry/references/series?search=RY-TEST");
        Assert.True(series!.Items.Single().IsTestWriteScope);
        Assert.Equal("RY-TEST",source.Search);
        Assert.Equal(HttpStatusCode.MethodNotAllowed,(await client.PostAsync(url,null)).StatusCode);
    }

    private sealed class References : ISalesOrderEntryReferences
    {
        public int Calls; public string? Search;
        public Task<PagedResult<OrderSeriesReferenceDto>> GetSeriesAsync(string? search,int page,int pageSize,bool includeHidden,CancellationToken ct)
        {
            Calls++;Search=search;
            if(page<1)throw new ArgumentOutOfRangeException(nameof(page));
            return Task.FromResult(new PagedResult<OrderSeriesReferenceDto>(new[]{new OrderSeriesReferenceDto(2136330188,"RY-TEST",7,1,-1,null,null,false,true)},1,page,pageSize));
        }
        public Task<IReadOnlyList<ProductCategoryReferenceDto>> GetCategoriesAsync(bool includeInactive,CancellationToken ct)=>Task.FromResult<IReadOnlyList<ProductCategoryReferenceDto>>([]);
        public Task<PagedResult<ProductTypeReferenceDto>> GetTypesAsync(int? categoryId,string? search,int page,int pageSize,bool includeInactive,CancellationToken ct)=>Task.FromResult(new PagedResult<ProductTypeReferenceDto>([],0,page,pageSize));
        public Task<ProductTypeDefinitionDto?> GetTypeDefinitionAsync(int typeId,CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(typeId!=-1025240863?null:new ProductTypeDefinitionDto(new ProductTypeReferenceDto(typeId,1172751775,"R25 LP",true,true),"Category",true,Enumerable.Range(1,12).Select(i=>new ProductParameterDefinitionDto(i,"Label","Width-45",null,i==12?ProductParameterStorageKind.Decimal:ProductParameterStorageKind.Text)).ToArray(),new ProductPricingDefinitionDto("any",-1789163110,0.25m,null,null,true)));
        }
    }
}
