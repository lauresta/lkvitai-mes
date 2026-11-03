using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.Modules.Sales.Api.Security;
using LKvitai.MES.Modules.Sales.WebUI.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace LKvitai.MES.Tests.Sales.Auth;

public sealed class SalesSsoIdentityTests
{
    private const string Authority = "https://sso.example.test/application/o/cikada/";
    private const string McpIssuer = "https://sso.example.test/application/o/cikada-mcp/";
    private static readonly CikadaOidcOptions Settings = new()
    { Enabled = true, Authority = Authority, ClientId = "web-client", Audiences = ["mcp-client"], Issuers = [McpIssuer] };

    [Fact]
    public void Sso_account_assertions_reject_untrusted_origin_metadata_tampering_and_expiry()
    {
        var clock = new Clock();
        var codec = new CikadaSsoAccountProof(new EphemeralDataProtectionProvider(), clock);
        var valid = SsoPrincipal("external-non-guid", "Cookies");
        var assertion = codec.Create(valid, Settings);
        Assert.False(valid.HasClaim(claim => claim.Type == "legacy_sql_login"));
        Assert.NotNull(assertion);
        Assert.Equal("bykovas", codec.Read(assertion, Settings)!.Username);
        // Shopfloor forwards the JWT through PortalStructuredBearer. Only the
        // validator's complete SSO metadata can produce this proof.
        Assert.NotNull(codec.Create(SsoPrincipal("external", PortalStructuredBearerAuthenticationDefaults.Scheme), Settings));
        Assert.Null(codec.Create(new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("auth_source", CikadaJwtValidator.AuthSource)],
            PortalStructuredBearerAuthenticationDefaults.Scheme)), Settings));
        var missing = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, "bykovas"), new Claim("auth_source", "oauth"),
            new Claim(SalesCredentialProtector.LocalAccountClaim, Guid.NewGuid().ToString())], "Cookies"));
        Assert.Null(codec.Create(missing, Settings));
        var noSubject = new ClaimsPrincipal(CikadaJwtValidator.BuildIdentity([
            new Claim("preferred_username", "bykovas"), new Claim("iss", Authority), new Claim("aud", "web-client"),
            new Claim(CikadaJwtValidator.SsoSubjectClaim, "attacker-supplied")], Settings, "Cookies"));
        Assert.False(noSubject.HasClaim(claim => claim.Type == CikadaJwtValidator.SsoSubjectClaim));
        Assert.Null(codec.Create(noSubject, Settings));
        Assert.Null(codec.Create(SsoPrincipal("external", "Cookies", "https://attacker.test/"), Settings));
        Assert.Null(codec.Create(SsoPrincipal("external", "Cookies", audience: "other-app"), Settings));
        Assert.Null(codec.Read(assertion + "tamper", Settings));
        clock.Now = clock.Now.AddMinutes(3);
        Assert.Null(codec.Read(assertion, Settings));
        Assert.Null(SalesCredentialProtector.Subject((ClaimsIdentity)SsoPrincipal(Guid.NewGuid().ToString(), "Cookies").Identity!));
    }

    [Theory]
    [InlineData("external-non-guid", true)]
    [InlineData("219414eb-af21-4da6-bb62-c33fab8cf348", true)]
    [InlineData("missing-account", false)]
    public async Task Cookie_proof_resolves_the_current_local_account_without_replacing_external_subject(string external, bool active)
    {
        var localId = Guid.NewGuid();
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer(); builder.Environment.EnvironmentName = "Test";
        AddSettings(builder.Configuration);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAuthentication("Cookies").AddCookie("Cookies");
        builder.Services.AddSalesSessionCredentials(builder.Configuration);
        builder.Services.AddAuthorization();
        builder.Services.AddHttpClient("CikadaUsers").ConfigurePrimaryHttpMessageHandler(sp => new Handler(request =>
        {
            var codec = sp.GetRequiredService<CikadaSsoAccountProof>();
            var asserted = codec.Read(request.Headers.GetValues(CikadaSsoAccountProof.HeaderName).Single(), Settings);
            Assert.NotNull(asserted); Assert.Equal(external, asserted!.Subject); Assert.Equal("bykovas", asserted.Username);
            return active ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new CikadaSsoAccount(localId, "bykovas")) }
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }));
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapSalesSessionProof();
        app.MapGet("/identity", (HttpContext context) => new
        {
            external = context.User.FindFirstValue(ClaimTypes.NameIdentifier),
            local = SalesCredentialProtector.Subject((ClaimsIdentity?)context.User.Identity),
            count = context.User.Identities.Count(identity => identity.IsAuthenticated)
        });
        await app.StartAsync(); var client = app.GetTestClient();
        var principal = SsoPrincipal(external, "Cookies");
        var identity = (ClaimsIdentity)principal.Identity!;
        // A cookie's Sales alias is never reused without a current active-account lookup.
        identity.AddClaim(new Claim(SalesCredentialProtector.LocalAccountClaim, Guid.NewGuid().ToString()));
        // Fresh OIDC cookies carry trusted metadata, so an expired API JWT does not
        // shorten the signed Portal cookie's lifetime or need to be accepted as a JWT.
        identity.AddClaim(new Claim(PortalOidcDefaults.AccessTokenClaim, "expired-jwt-must-not-be-used"));
        var options = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get("Cookies");
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties
        { IssuedUtc = DateTimeOffset.UtcNow, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1) }, "Cookies");
        client.DefaultRequestHeaders.Add("Cookie", options.Cookie.Name + "=" + options.TicketDataFormat.Protect(ticket));
        var response = await client.GetAsync("/auth/session-proof");
        Assert.Equal(active ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, response.StatusCode);
        if (active)
        {
            var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var codec = app.Services.GetRequiredService<SalesCredentialProtector>();
            Assert.Equal(localId, codec.ReadSession(body.GetProperty("proof").GetString())!.Subject);
        }
        var displayed = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/identity");
        Assert.Equal(external, displayed.GetProperty("external").GetString());
        Assert.Equal(1, displayed.GetProperty("count").GetInt32());
        if (active) Assert.Equal(localId, displayed.GetProperty("local").GetGuid());
        else Assert.Equal(System.Text.Json.JsonValueKind.Null, displayed.GetProperty("local").ValueKind);
    }

    [Theory]
    [InlineData("web-client", Authority)]
    [InlineData("mcp-client", McpIssuer)]
    public async Task Direct_SSO_and_MCP_JWTs_use_server_resolved_actor_and_never_unsigned_legacy_headers(string audience, string issuer)
    {
        using var rsa = RSA.Create(2048); var signingKey = new RsaSecurityKey(rsa) { KeyId = "test-key" };
        var localId = Guid.NewGuid(); var external = Guid.NewGuid().ToString();
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer(); builder.Environment.EnvironmentName = "Test";
        AddSettings(builder.Configuration);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddSalesProtectedAuthentication(builder.Configuration);
        builder.Services.AddHttpClient("CikadaUsers", client => client.BaseAddress = new Uri("http://users.test"))
            .ConfigurePrimaryHttpMessageHandler(sp => new Handler(request =>
            {
                var asserted = sp.GetRequiredService<CikadaSsoAccountProof>().Read(request.Headers.GetValues(CikadaSsoAccountProof.HeaderName).Single(), Settings);
                Assert.NotNull(asserted); Assert.Equal("bykovas", asserted!.Username);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new CikadaSsoAccount(localId, "bykovas")) };
            }));
        await using var app = builder.Build();
        var validator = app.Services.GetRequiredService<CikadaJwtValidator>();
        var configuration = new OpenIdConnectConfiguration { Issuer = Authority }; configuration.SigningKeys.Add(signingKey);
        var manager = new ConfigurationManager<OpenIdConnectConfiguration>(Authority + ".well-known/openid-configuration", new Retriever(configuration));
        typeof(CikadaJwtValidator).GetField("_manager", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(validator, manager);
        typeof(CikadaJwtValidator).GetField("_managerAuthority", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(validator, Authority);
        app.UseAuthentication(); app.UseAuthorization();
        app.MapGet("/actor", (HttpContext context) => new
        {
            actor = SalesCredentialProtector.Subject((ClaimsIdentity)context.User.Identity!),
            external = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        }).RequireAuthorization(SalesCredentialProtector.Policy);
        await app.StartAsync(); var client = app.GetTestClient();
        string Token(string targetAudience, DateTime expires) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            issuer, targetAudience,
            [new Claim("sub", external), new Claim("preferred_username", "bykovas"),
                new Claim(SalesCredentialProtector.LocalAccountClaim, Guid.NewGuid().ToString()), new Claim("legacy_sql_login", "sa")],
            DateTime.UtcNow.AddMinutes(-10), expires, new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256)));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(audience, DateTime.UtcNow.AddMinutes(5)));
        var body = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/actor");
        Assert.Equal(localId, body.GetProperty("actor").GetGuid()); Assert.Equal(external, body.GetProperty("external").GetString());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"{localId}|WarehouseAdmin|exp=9999999999|source=oauth");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/actor")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token("wrong-audience", DateTime.UtcNow.AddMinutes(5)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/actor")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(audience, DateTime.UtcNow.AddMinutes(-5)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/actor")).StatusCode);
    }

    private static ClaimsPrincipal SsoPrincipal(string subject, string scheme, string issuer = Authority, string audience = "web-client")
        => new(CikadaJwtValidator.BuildIdentity([new Claim("sub", subject), new Claim("preferred_username", "bykovas"),
            new Claim("iss", issuer), new Claim("aud", audience), new Claim("legacy_sql_login", "sa")], Settings, scheme));
    private static void AddSettings(ConfigurationManager configuration) => configuration.AddInMemoryCollection(new Dictionary<string,string?>
    {
        ["Oidc:Enabled"] = "true", ["Oidc:Authority"] = Authority, ["Oidc:ClientId"] = "web-client",
        ["Oidc:Audiences:0"] = "mcp-client", ["Oidc:Issuers:0"] = McpIssuer, ["CikadaUsers:BaseUrl"] = "http://users.test"
    });
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) => Task.FromResult(send(request)); }
    private sealed class Retriever(OpenIdConnectConfiguration configuration) : IConfigurationRetriever<OpenIdConnectConfiguration>
    { public Task<OpenIdConnectConfiguration> GetConfigurationAsync(string address,IDocumentRetriever retriever,CancellationToken cancel) => Task.FromResult(configuration); }
    private sealed class Clock : TimeProvider
    { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
}
