using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using LKvitai.MES.BuildingBlocks.Manuals;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace LKvitai.MES.Tests.Sales.Auth;

/// <summary>The /manuals endpoint of the Portal WebUI: signed-in users only, no way out of the manuals site.</summary>
public sealed class ManualsProxyTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("sales/", "sales/")]
    [InlineData("search/search_index.json", "search/search_index.json")]
    [InlineData("img/warehouse/inbound.png", "img/warehouse/inbound.png")]
    public void NormalizePath_keeps_ordinary_manual_paths(string? input, string expected) =>
        Assert.Equal(expected, ManualsProxy.NormalizePath(input));

    [Theory]
    [InlineData("..")]
    [InlineData("../secret")]
    [InlineData("a/../../b")]
    [InlineData("a/./b")]
    [InlineData("a//b")]
    [InlineData("a\\b")]
    [InlineData("%2e%2e/b")]
    [InlineData("a/%2e%2e/b")]
    [InlineData("http://evil.test/")]
    [InlineData("a\0b")]
    [InlineData("a\nb")]
    public void NormalizePath_refuses_anything_that_could_leave_the_site(string input) =>
        Assert.Null(ManualsProxy.NormalizePath(input));

    [Theory]
    [InlineData("", false)]
    [InlineData("sales/", false)]
    [InlineData("sales", true)]
    [InlineData("sales/orders", true)]
    [InlineData("search/search_index.json", false)]
    [InlineData("assets/app.css", false)]
    public void NeedsTrailingSlash_only_for_directory_urls_without_the_slash(string path, bool expected) =>
        Assert.Equal(expected, ManualsProxy.NeedsTrailingSlash(path));

    [Fact]
    public async Task Anonymous_requests_are_rejected_and_never_reach_the_manuals_container()
    {
        await using var host = await StartAsync();
        var client = host.App.GetTestClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/manuals/")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/manuals/search/search_index.json")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/manuals/img/x.png")).StatusCode);
        Assert.Empty(host.Upstream.Requests);
    }

    [Fact]
    public async Task Signed_in_users_get_the_file_streamed_from_the_manuals_container()
    {
        await using var host = await StartAsync();
        var client = SignedIn(host);

        var response = await client.GetAsync("/manuals/search/search_index.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("{\"docs\":[]}", await response.Content.ReadAsStringAsync());
        Assert.Contains("private", response.Headers.CacheControl?.ToString());
        Assert.Equal("http://manuals.test/search/search_index.json", Assert.Single(host.Upstream.Requests));
    }

    [Fact]
    public async Task The_site_root_is_requested_for_manuals_slash_and_manuals_redirects_to_it()
    {
        await using var host = await StartAsync();
        var client = SignedIn(host);

        var redirect = await client.GetAsync("/manuals");
        Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);
        Assert.Equal("/manuals/", redirect.Headers.Location?.OriginalString);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/manuals/")).StatusCode);
        Assert.Equal("http://manuals.test/", Assert.Single(host.Upstream.Requests));
    }

    [Fact]
    public async Task Directory_urls_without_a_trailing_slash_are_redirected_without_calling_upstream()
    {
        await using var host = await StartAsync();
        var client = SignedIn(host);

        var response = await client.GetAsync("/manuals/sales/orders?x=1");

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal("/manuals/sales/orders/?x=1", response.Headers.Location?.OriginalString);
        Assert.Empty(host.Upstream.Requests);
    }

    [Theory]
    [InlineData("/manuals/..%2f..%2fetc/passwd")]
    [InlineData("/manuals/%2e%2e/secret")]
    [InlineData("/manuals/a%5c..%5cb")]
    [InlineData("/manuals/a/%252e%252e/b")]
    public async Task Path_traversal_attempts_are_not_forwarded(string url)
    {
        await using var host = await StartAsync();
        var client = SignedIn(host);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(host.Upstream.Requests);
    }

    [Fact]
    public async Task Missing_files_are_404_and_an_unreachable_container_is_502()
    {
        await using var host = await StartAsync();
        var client = SignedIn(host);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/manuals/nope.html")).StatusCode);
        host.Upstream.Fail = true;
        Assert.Equal(HttpStatusCode.BadGateway, (await client.GetAsync("/manuals/index.html")).StatusCode);
    }

    [Fact]
    public async Task Without_a_configured_base_url_the_endpoint_answers_404()
    {
        await using var host = await StartAsync(baseUrl: null);

        Assert.Equal(HttpStatusCode.NotFound, (await SignedIn(host).GetAsync("/manuals/")).StatusCode);
        Assert.Empty(host.Upstream.Requests);
    }

    private static HttpClient SignedIn(TestHost host)
    {
        var client = host.App.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuth.Header, "user");
        return client;
    }

    private static async Task<TestHost> StartAsync(string? baseUrl = "http://manuals.test/")
    {
        var upstream = new FakeManuals();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { [ManualsProxy.BaseUrlConfigKey] = baseUrl });
        builder.Services.AddAuthentication(TestAuth.Scheme).AddScheme<AuthenticationSchemeOptions, TestAuth>(TestAuth.Scheme, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddManualsProxy(builder.Configuration);
        builder.Services.AddHttpClient(ManualsProxy.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => upstream);
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapManualsProxy();
        await app.StartAsync();
        return new TestHost(app, upstream);
    }

    private sealed class TestHost(WebApplication app, FakeManuals upstream) : IAsyncDisposable
    {
        public WebApplication App { get; } = app;
        public FakeManuals Upstream { get; } = upstream;
        public ValueTask DisposeAsync() => App.DisposeAsync();
    }

    private sealed class FakeManuals : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        public bool Fail { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.ToString());
            if (Fail) throw new HttpRequestException("connection refused");
            var path = request.RequestUri.AbsolutePath;
            if (path.EndsWith("nope.html", StringComparison.Ordinal)) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            var json = path.EndsWith(".json", StringComparison.Ordinal);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json ? "{\"docs\":[]}" : "<html>Naudotojo gidas</html>", System.Text.Encoding.UTF8, json ? "application/json" : "text/html"),
            });
        }
    }

    /// <summary>Signs a request in when it carries the test header; challenges with 401 otherwise.</summary>
    private sealed class TestAuth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Scheme = "Test";
        public const string Header = "X-Test-User";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey(Header)) return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "user")], Scheme);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
        }
    }
}
