using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using LKvitai.MES.Modules.Sales.Api.Security;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.Modules.Sales.WebUI.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using LKvitai.MES.Modules.Sales.Contracts.Common;
using LKvitai.MES.Modules.Sales.Contracts.Orders;
using LKvitai.MES.Modules.Sales.Infrastructure.Stub;
using Xunit;

namespace LKvitai.MES.Tests.Sales.Auth;

public sealed class SalesAuthenticationTests
{
    [Fact]
    public void Credentials_are_cryptographic_bounded_and_purpose_specific()
    {
        var provider = new EphemeralDataProtectionProvider();
        var clock = new Clock();
        var codec = new SalesCredentialProtector(provider, clock);
        var proof = codec.CreateSession(Guid.NewGuid(), "Employee", clock.GetUtcNow().AddMinutes(8));
        var token = codec.CreateApi(proof);
        Assert.NotNull(codec.ReadApi(token));
        Assert.Null(codec.ReadApi(proof));
        Assert.Null(codec.ReadSession(token));
        Assert.Null(codec.ReadApi(token![..^4] + "0000"));
        Assert.Null(new SalesCredentialProtector(new EphemeralDataProtectionProvider(), clock).ReadApi(token));
        clock.Now = clock.Now.AddMinutes(2);
        Assert.Null(codec.ReadApi(token));
        Assert.NotNull(codec.CreateApi(proof));
        clock.Now = clock.Now.AddMinutes(1);
        Assert.Null(codec.CreateApi(proof));
    }

    [Theory]
    [InlineData("audience")]
    [InlineData("source")]
    [InlineData("empty-subject")]
    [InlineData("future")]
    [InlineData("long-lived")]
    [InlineData("version")]
    [InlineData("missing-expiry")]
    public void Even_protected_invalid_payloads_are_rejected(string mutation)
    {
        var provider = new EphemeralDataProtectionProvider();
        var clock = new Clock();
        var codec = new SalesCredentialProtector(provider, clock);
        var p = codec.ReadSession(codec.CreateSession(Guid.NewGuid(), "Employee", clock.GetUtcNow().AddHours(1)))!;
        p = mutation switch
        {
            "audience" => p with { Audience = "warehouse" },
            "source" => p with { Source = "dev" },
            "empty-subject" => p with { Subject = Guid.Empty },
            "future" => p with { Issued = clock.Now.AddMinutes(1), Expires = clock.Now.AddMinutes(3) },
            "long-lived" => p with { Expires = clock.Now.AddHours(1) },
            "version" => p with { Version = 2 },
            _ => p with { Expires = default }
        };
        var invalid = provider.CreateProtector("LKvitai.MES.Sales.PortalSession.v1").Protect(JsonSerializer.Serialize(p));
        Assert.Null(codec.ReadSession(invalid));
        Assert.Null(codec.CreateApi(invalid));
    }

    [Fact]
    public async Task Circuit_subjects_and_requests_cannot_exchange_credentials()
    {
        var clock = new Clock();
        var codec = new SalesCredentialProtector(new EphemeralDataProtectionProvider(), clock);
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var proofA = codec.CreateSession(a, "A", clock.Now.AddHours(1));
        var proofB = codec.CreateSession(b, "B", clock.Now.AddHours(1));
        var authA = new State(User(a, proofA)); var authB = new State(User(b, proofB));
        var serviceA = new SalesSessionCredentials(authA, codec);
        var serviceB = new SalesSessionCredentials(authB, codec);
        for (var i = 0; i < 20; i++)
        {
            var results = await Task.WhenAll(serviceA.CreateApiCredentialAsync(), serviceB.CreateApiCredentialAsync());
            Assert.Equal(a, codec.ReadApi(results[0])!.Subject);
            Assert.Equal(b, codec.ReadApi(results[1])!.Subject);
        }
        Assert.False(await serviceA.AcceptProofAsync(proofB));
        authA.User = User(b, proofB);
        Assert.Null(await serviceA.CreateApiCredentialAsync());
        authB.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sales_review", "true") }, "SalesReviewCookie"));
        Assert.Null(await serviceB.CreateApiCredentialAsync());
        Assert.False(await serviceB.AcceptProofAsync(proofA));
        var forged = new SalesSessionCredentials(new State(User(a, "unprotected-string")), codec);
        Assert.Null(await forged.CreateApiCredentialAsync());
    }

    [Fact]
    public async Task Actual_api_clients_attach_each_circuit_actor_and_review_never_calls_transport()
    {
        var clock = new Clock();
        var codec = new SalesCredentialProtector(new EphemeralDataProtectionProvider(), clock);
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var transport = new CaptureTransport(codec);
        var factory = new Factory(new HttpClient(transport) { BaseAddress = new Uri("http://test.invalid") });
        SalesApiClient Client(Guid subject)
        {
            var state = new State(User(subject, codec.CreateSession(subject, "Employee", clock.Now.AddHours(1))));
            return new SalesApiClient(factory, NullLogger<SalesApiClient>.Instance, state,
                new LKvitai.MES.Modules.Sales.Contracts.Samples.SalesSampleOrders(), new SalesSessionCredentials(state, codec));
        }
        var clientA = Client(a); var clientB = Client(b);
        await Task.WhenAll(Enumerable.Range(0, 20).SelectMany(_ => new[]
        { clientA.GetOrdersAsync(new OrdersQueryParams(), default), clientB.GetOrdersAsync(new OrdersQueryParams(), default) }));
        Assert.Equal(20, transport.Subjects.Count(x => x == a));
        Assert.Equal(20, transport.Subjects.Count(x => x == b));
        var review = new State(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sales_review", "true") }, "SalesReviewCookie")));
        var reviewer = new SalesApiClient(factory, NullLogger<SalesApiClient>.Instance, review,
            new LKvitai.MES.Modules.Sales.Contracts.Samples.SalesSampleOrders(), new SalesSessionCredentials(review, codec));
        var samples = await reviewer.GetOrdersAsync(new OrdersQueryParams(), default);
        Assert.NotEmpty(samples!.Items);
        Assert.Equal(40, transport.Subjects.Count);
        clock.Now = clock.Now.AddMinutes(4);
        Assert.Null(await clientA.GetOrdersAsync(new OrdersQueryParams(), default));
        Assert.Equal(40, transport.Subjects.Count);
    }

    [Fact]
    public async Task Cookie_refresh_and_api_policy_enforce_the_actual_trust_boundary()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Environment.EnvironmentName = "Test";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        { ["SalesReview:Key"] = new string('a', 64), ["SalesReview:ExpiresAtUtc"] = DateTimeOffset.UtcNow.AddHours(1).ToString("O") });
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAuthentication("Cookies").AddCookie("Cookies");
        builder.Services.AddSalesReviewAccess(builder.Environment, builder.Configuration);
        builder.Services.AddSalesSessionCredentials();
        builder.Services.AddSalesProtectedAuthentication();
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization();
        app.MapSalesSessionProof();
        app.MapGet("/api/sales/actor", (HttpContext context) => context.User.FindFirstValue(ClaimTypes.NameIdentifier))
            .RequireAuthorization(SalesCredentialProtector.Policy);
        await app.StartAsync();
        var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/sales/actor")).StatusCode);
        var fake = new HttpRequestMessage(HttpMethod.Get, "/api/sales/actor");
        fake.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "attacker|Admin");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(fake)).StatusCode);
        var subject = Guid.NewGuid();
        var cookieOptions = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get("Cookies");
        var ticket = new AuthenticationTicket(User(subject), new AuthenticationProperties
        { IssuedUtc = DateTimeOffset.UtcNow, ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(10) }, "Cookies");
        client.DefaultRequestHeaders.Add("Cookie", cookieOptions.Cookie.Name + "=" + cookieOptions.TicketDataFormat.Protect(ticket));
        // An otherwise legitimate Portal cookie must not authorize the API directly.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/sales/actor")).StatusCode);
        var refreshed = await client.GetAsync("/auth/session-proof");
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        Assert.True(refreshed.Headers.CacheControl!.NoStore);
        var body = await refreshed.Content.ReadFromJsonAsync<JsonElement>();
        var codec = app.Services.GetRequiredService<SalesCredentialProtector>();
        var proof = body.GetProperty("proof").GetString();
        Assert.Equal(subject, codec.ReadSession(proof)!.Subject);
        var api = new HttpRequestMessage(HttpMethod.Get, "/api/sales/actor");
        api.Headers.Authorization = new AuthenticationHeaderValue("Bearer", codec.CreateApi(proof));
        var result = await client.SendAsync(api);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(subject.ToString(), await result.Content.ReadAsStringAsync());
        var foreign = new HttpRequestMessage(HttpMethod.Get, "/auth/session-proof");
        foreign.Headers.Add("Sec-Fetch-Site", "cross-site");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(foreign)).StatusCode);
        // Both valid cookies must retain the selected review identity; never elevate via explicit Portal auth.
        var reviewOptions = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(SalesReviewAccess.CookieScheme);
        var reviewTicket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "sales-review"), new Claim("sales_review", "true"),
            new Claim("sales_review_key_id", Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Convert.FromHexString(new string('a',64)))))
        }, SalesReviewAccess.CookieScheme)), new AuthenticationProperties
        { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(10) }, SalesReviewAccess.CookieScheme);
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", cookieOptions.Cookie.Name + "=" + cookieOptions.TicketDataFormat.Protect(ticket)
            + "; " + SalesReviewAccess.CookieName + "=" + reviewOptions.TicketDataFormat.Protect(reviewTicket));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/auth/session-proof")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/sales/actor")).StatusCode);
    }

    private sealed class Factory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
    private sealed class CaptureTransport(SalesCredentialProtector codec) : HttpMessageHandler
    {
        public ConcurrentBag<Guid> Subjects { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var payload = codec.ReadApi(request.Headers.Authorization?.Parameter);
            Assert.NotNull(payload);
            Subjects.Add(payload.Subject);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = JsonContent.Create(new PagedResult<OrderSummaryDto>([], 0, 1, 100)) });
        }
    }

    private static ClaimsPrincipal User(Guid id, string? proof = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, id.ToString()), new(ClaimTypes.Name, "Employee") };
        if (proof is not null) claims.Add(new Claim(SalesCredentialProtector.SessionClaim, proof));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies"));
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class State(ClaimsPrincipal user) : AuthenticationStateProvider
    {
        public ClaimsPrincipal User = user;
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(User));
    }
}
