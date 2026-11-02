using System.Net;
using System.Security.Claims;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.Modules.Sales.Api.Security;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace LKvitai.MES.Tests.Sales.Auth;
public sealed class CikadaUserBridgeTests
{
    [Fact]
    public void Proofs_enforce_purpose_expiry_and_integrity()
    {
        var clock=new Clock();var codec=new CikadaUserBridgeProof(new EphemeralDataProtectionProvider(),clock);
        var actor=Guid.NewGuid();var token=codec.Create(actor,CikadaUserBridgeProof.LookupLegacySql);
        Assert.Equal(actor,codec.Read(token,CikadaUserBridgeProof.LookupLegacySql)!.UserId);
        Assert.Null(codec.Read(token,CikadaUserBridgeProof.ManageUsers));
        Assert.Null(codec.Read(token+"tamper",CikadaUserBridgeProof.LookupLegacySql));
        clock.Now=clock.Now.AddMinutes(3);
        Assert.Null(codec.Read(token,CikadaUserBridgeProof.LookupLegacySql));
    }
    [Theory]
    [InlineData(404,"LEGACY_MAPPING_REQUIRED",403)]
    [InlineData(403,"LEGACY_MAPPING_REQUIRED",403)]
    [InlineData(500,"IDENTITY_SERVICE_UNAVAILABLE",503)]
    public async Task Missing_inactive_and_unavailable_profiles_never_fall_back_to_sa(int status,string code,int expectedStatus)
    {
        var context=new DefaultHttpContext{User=Principal(Guid.NewGuid())};
        var transport=new Handler(_=>new HttpResponseMessage((HttpStatusCode)status));
        var resolver=Resolver(context,transport);
        var error=await Assert.ThrowsAsync<OrderEntryException>(()=>resolver.ResolveAsync(default));
        Assert.Equal(code,error.Code);Assert.Equal(expectedStatus,error.Status);
    }
    [Fact]
    public async Task Resolver_uses_verified_actor_and_rejects_foreign_mapping_response()
    {
        var actor=Guid.NewGuid();var codec=new CikadaUserBridgeProof(new EphemeralDataProtectionProvider(),TimeProvider.System);
        var context=new DefaultHttpContext{User=Principal(actor)};
        var transport=new Handler(request=>
        {
            var proof=request.Headers.GetValues(CikadaUserBridgeProof.HeaderName).Single();
            Assert.Equal(actor,codec.Read(proof,CikadaUserBridgeProof.LookupLegacySql)!.UserId);
            return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"userId\":\""+Guid.NewGuid()+"\",\"legacySqlLogin\":\"sa\"}",System.Text.Encoding.UTF8,"application/json")};
        });
        var resolver=new CikadaLegacySqlIdentityResolver(new HttpContextAccessor{HttpContext=context},new Factory(new HttpClient(transport){BaseAddress=new Uri("http://users.test")}),codec);
        Assert.Equal(403,(await Assert.ThrowsAsync<OrderEntryException>(()=>resolver.ResolveAsync(default))).Status);
    }
    [Theory]
    [InlineData("Header")]
    [InlineData("Cookies")]
    [InlineData("SalesReviewCookie")]
    public async Task Non_Sales_protected_identity_cannot_select_a_SQL_login(string authenticationType)
    {
        var context=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,Guid.NewGuid().ToString())],authenticationType))};
        var transport=new Handler(_=>throw new InvalidOperationException("Must not call user lookup"));
        Assert.Equal(401,(await Assert.ThrowsAsync<OrderEntryException>(()=>Resolver(context,transport).ResolveAsync(default))).Status);
    }
    private static ClaimsPrincipal Principal(Guid id)=>new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,id.ToString())],SalesCredentialProtector.Scheme));
    private static CikadaLegacySqlIdentityResolver Resolver(HttpContext context,HttpMessageHandler transport)=>new(new HttpContextAccessor{HttpContext=context},new Factory(new HttpClient(transport){BaseAddress=new Uri("http://users.test")}),new CikadaUserBridgeProof(new EphemeralDataProtectionProvider(),TimeProvider.System));
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> action):HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>Task.FromResult(action(request)); }
    private sealed class Factory(HttpClient client):IHttpClientFactory{public HttpClient CreateClient(string name)=>client;}
    private sealed class Clock:TimeProvider{public DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now;}
}
