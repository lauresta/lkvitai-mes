using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.Modules.Warehouse.WebUI.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public sealed class AdminUsersSsoClientTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Sso_admin_client_uses_current_durable_id_and_never_external_guid_or_alias(bool accountExists)
    {
        var settings = new CikadaOidcOptions { Enabled = true, Authority = "https://sso.test/", ClientId = "cikada" };
        var options = Mock.Of<IOptionsMonitor<CikadaOidcOptions>>(o => o.CurrentValue == settings);
        var localId = Guid.NewGuid(); var external = Guid.NewGuid().ToString();
        var identity = CikadaJwtValidator.BuildIdentity([
            new Claim("sub", external), new Claim("preferred_username", "bykovas"), new Claim("iss", settings.Authority),
            new Claim("aud", settings.ClientId)], settings, "Cookies");
        identity.AddClaim(new Claim("warehouse_access_token", "validated-access-jwt"));
        identity.AddClaim(new Claim(SalesCredentialProtector.LocalAccountClaim, Guid.NewGuid().ToString()));
        var protection = new EphemeralDataProtectionProvider();
        var accountProof = new CikadaSsoAccountProof(protection, TimeProvider.System);
        var adminProof = new CikadaUserBridgeProof(protection, TimeProvider.System);
        var adminCalled = false;
        var factory = new Factory(name => new HttpClient(new Handler(request =>
        {
            if (name == "CikadaUsers")
            {
                var caller = accountProof.Read(request.Headers.GetValues(CikadaSsoAccountProof.HeaderName).Single(), settings);
                Assert.Equal(external, caller!.Subject); Assert.Equal("bykovas", caller.Username);
                return accountExists ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new CikadaSsoAccount(localId, "bykovas")) }
                    : new HttpResponseMessage(HttpStatusCode.NotFound);
            }
            Assert.Equal("WarehouseUserAdminApi", name); adminCalled = true;
            Assert.Equal(localId, adminProof.Read(request.Headers.GetValues(CikadaUserBridgeProof.HeaderName).Single(), CikadaUserBridgeProof.ManageUsers)!.UserId);
            Assert.Equal("validated-access-jwt", request.Headers.Authorization!.Parameter);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") };
        })) { BaseAddress = new Uri("http://warehouse.test/") });
        var lookup = new CikadaSsoAccountLookup(factory, accountProof, options);
        var client = new AdminUsersClient(factory, new Authentication(new ClaimsPrincipal(identity)), adminProof, ssoAccounts: lookup);
        if (accountExists) Assert.Empty(await client.GetUsersAsync());
        else await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetUsersAsync());
        Assert.Equal(accountExists, adminCalled);
        Assert.Equal(external, identity.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    }

    private sealed class Authentication(ClaimsPrincipal user) : AuthenticationStateProvider
    { public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(user)); }
    private sealed class Factory(Func<string, HttpClient> create) : IHttpClientFactory
    { public HttpClient CreateClient(string name) => create(name); }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(send(request)); }
}
