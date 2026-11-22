using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.BuildingBlocks.Search;
using LKvitai.MES.BuildingBlocks.WebUI.Components;
using LKvitai.MES.Modules.Sales.Api.Endpoints;
using LKvitai.MES.Modules.Sales.Api.Security;
using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Contracts.Common;
using LKvitai.MES.Modules.Sales.Contracts.Orders;
using LKvitai.MES.Modules.Sales.WebUI.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using Xunit;

namespace LKvitai.MES.Tests.Sales.Auth;

/// <summary>Portal "Search everything" (issue #375): Sales API endpoint, Sales WebUI relay and the shell panel markup.</summary>
public sealed class UniversalSearchTests
{
    private static OrderSummaryDto Order(long id, string number, string customer, bool restricted = false) => new(
        id, number, new DateOnly(2026, 11, 2), 100m, 0m, false, customer, false, false, false,
        "Įvestas", OrderStatusCodes.Entered, "Store A", "Street 1", null, restricted);

    [Fact]
    public async Task Api_search_has_the_orders_list_policy_and_projects_the_orders_list_query()
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddSalesProtectedAuthentication();
        var orders = new Orders(
            Order(1, "TS-10-A", "Sample Buyer"),
            Order(2, "TS-11-A", "Other Company"),
            Order(3, "TS-12/B", "Sample Buyer"),
            Order(4, "TS-13-A", "Sample Hidden", restricted: true));
        builder.Services.AddSingleton<IOrdersQueryService>(orders);
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization();
        var group = app.MapGroup("/api/sales").RequireAuthorization(SalesCredentialProtector.Policy);
        group.MapOrdersEndpoints(); group.MapSearchEndpoints();
        await app.StartAsync(); var client = app.GetTestClient();

        // Same answer as the list endpoint for a caller without the Sales credential, and no read.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/sales/orders?search=sample")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/sales/search?q=sample")).StatusCode);
        Assert.Empty(orders.Queries);

        var codec = app.Services.GetRequiredService<SalesCredentialProtector>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            codec.CreateApi(codec.CreateSession(Guid.NewGuid(), "Employee", DateTimeOffset.UtcNow.AddHours(1))));

        // Below the minimum length nothing is queried.
        var shortResponse = await client.GetAsync("/api/sales/search?q=s");
        Assert.Equal(HttpStatusCode.OK, shortResponse.StatusCode);
        Assert.Equal("[]", await shortResponse.Content.ReadAsStringAsync());
        Assert.Empty(orders.Queries);

        var response = await client.GetAsync("/api/sales/search?q=%20sample%20&limit=99");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var first = json[0];
        Assert.Equal(["kind", "title", "subtitle", "url", "score"], first.EnumerateObject().Select(p => p.Name).ToArray());

        var query = Assert.Single(orders.Queries);
        Assert.Equal("sample", query.Search); Assert.Equal(1, query.Page); Assert.Equal(SearchEndpoints.OrdersWindow, query.PageSize);
        Assert.Null(query.Status); Assert.Null(query.Store); Assert.False(query.HasDebt);

        var hits = json.Deserialize<List<SearchResultDto>>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var orderHits = hits.Where(h => h.Kind == SearchKinds.Order).ToList();
        Assert.Equal(["TS-10-A", "TS-11-A", "TS-12/B", "TS-13-A"], orderHits.Select(h => h.Title).ToArray());
        Assert.Equal("orders/TS-12%2FB", orderHits[2].Url);
        Assert.Equal("Sample Buyer · Įvestas · Store A", orderHits[0].Subtitle);
        // The restricted order keeps what the list row shows; it never becomes a customer hit.
        var customer = Assert.Single(hits, h => h.Kind == SearchKinds.Customer);
        Assert.Equal("Sample Buyer", customer.Title); Assert.Null(customer.Subtitle);
        Assert.Equal("?search=Sample%20Buyer", customer.Url);
        Assert.DoesNotContain(hits, h => h.Kind == SearchKinds.Customer && h.Title.Contains("Hidden"));
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.PostAsync("/api/sales/search?q=sample", null)).StatusCode);
    }

    [Fact]
    public async Task Api_search_limits_each_kind_and_puts_number_matches_first()
    {
        var rows = Enumerable.Range(1, 20).Select(i => Order(i, $"AA-{i}-X", $"Client {i} TS")).ToList();
        rows.Add(Order(99, "TS-7-A", "Somebody"));
        var orders = new Orders(rows.ToArray());

        var hits = await SearchEndpoints.SearchAsync(orders, "ts", null, CancellationToken.None);

        Assert.Equal(SearchRequest.DefaultLimit, hits.Count(h => h.Kind == SearchKinds.Order));
        Assert.Equal(SearchRequest.DefaultLimit, hits.Count(h => h.Kind == SearchKinds.Customer));
        Assert.Equal("TS-7-A", hits[0].Title);
        Assert.Equal(SearchRequest.MaxLimit, (await SearchEndpoints.SearchAsync(orders, "ts", 50, CancellationToken.None)).Count(h => h.Kind == SearchKinds.Order));
        Assert.Single(await SearchEndpoints.SearchAsync(orders, "ts", 0, CancellationToken.None), h => h.Kind == SearchKinds.Order);
        Assert.Empty(await SearchEndpoints.SearchAsync(orders, "  ", null, CancellationToken.None));
    }

    [Fact]
    public async Task WebUi_relay_needs_the_portal_cookie_calls_the_api_as_the_user_and_adds_screens()
    {
        var transport = new Transport();
        await using var app = await RelayHostAsync(transport);
        var client = app.GetTestClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/sales/shell/search?q=sample")).StatusCode);
        Assert.Empty(transport.Requests);

        var subject = Guid.NewGuid();
        SignIn(app, client, subject);
        var foreign = new HttpRequestMessage(HttpMethod.Get, "/sales/shell/search?q=sample");
        foreign.Headers.Add("Sec-Fetch-Site", "cross-site");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(foreign)).StatusCode);
        Assert.Equal("[]", await (await client.GetAsync("/sales/shell/search?q=s")).Content.ReadAsStringAsync());
        Assert.Empty(transport.Requests);

        transport.Body = [new(SearchKinds.Order, "TS-10-A", "Sample Buyer", "orders/TS-10-A", 0.8),
            new(SearchKinds.Customer, "Sample Buyer", null, "?search=Sample%20Buyer", 0.9),
            new(SearchKinds.Order, "bad", null, "https://elsewhere.example/x", 1)];
        var response = await client.GetAsync("/sales/shell/search?q=orders&limit=5");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.False(response.Headers.Contains(SearchRequest.PartialHeader));
        var hits = (await response.Content.ReadFromJsonAsync<List<SearchResultDto>>())!;
        Assert.Equal(["/sales/orders/TS-10-A", "/sales/?search=Sample%20Buyer", "/sales/"], hits.Select(h => h.Url).ToArray());
        Assert.Equal(SearchKinds.Screen, hits[^1].Kind); Assert.Equal("Orders", hits[^1].Title);

        var relayed = Assert.Single(transport.Requests);
        Assert.Equal("/api/sales/search?q=orders&limit=5", relayed.PathAndQuery);
        var codec = app.Services.GetRequiredService<SalesCredentialProtector>();
        Assert.Equal(subject, codec.ReadApi(relayed.Bearer)!.Subject);

        // Lithuanian menu label without diacritics, while the UI language is English.
        var lithuanian = (await (await client.GetAsync("/sales/shell/search?q=uzsakym")).Content.ReadFromJsonAsync<List<SearchResultDto>>())!;
        Assert.Contains(lithuanian, h => h.Kind == SearchKinds.Screen && h.Url == "/sales/");
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, HttpStatusCode.Forbidden, false)]
    [InlineData(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, false)]
    [InlineData(HttpStatusCode.InternalServerError, HttpStatusCode.OK, true)]
    public async Task WebUi_relay_hides_the_module_without_access_and_degrades_to_screens_when_the_api_fails(
        HttpStatusCode api, HttpStatusCode expected, bool partial)
    {
        var transport = new Transport { Status = api };
        await using var app = await RelayHostAsync(transport);
        var client = app.GetTestClient();
        SignIn(app, client, Guid.NewGuid());

        var response = await client.GetAsync("/sales/shell/search?q=orders");

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(partial, response.Headers.Contains(SearchRequest.PartialHeader));
        if (partial)
        {
            var hits = (await response.Content.ReadFromJsonAsync<List<SearchResultDto>>())!;
            Assert.All(hits, h => Assert.Equal(SearchKinds.Screen, h.Kind)); Assert.NotEmpty(hits);
        }
    }

    [Theory]
    [InlineData("en", "Search everything…", "Orders", "Nothing found.")]
    [InlineData("lt", "Ieškoti visur…", "Užsakymai", "Nieko nerasta.")]
    public async Task Shell_renders_a_closed_search_panel_and_hands_the_script_localized_texts_and_endpoints(
        string language, string placeholder, string ordersHeading, string empty)
    {
        var original = CultureInfo.CurrentUICulture; CultureInfo.CurrentUICulture = new(language);
        try
        {
            var js = new RecordingJs();
            var registry = new ServiceCollection(); registry.AddLogging(); registry.AddLocalization();
            registry.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            registry.AddSingleton<NavigationManager, FixedNavigation>();
            registry.AddSingleton<IJSRuntime>(js);
            await using var services = registry.BuildServiceProvider();
            await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
            var html = await renderer.Dispatcher.InvokeAsync(async () => WebUtility.HtmlDecode(
                (await renderer.RenderComponentAsync<PortalModuleShell>()).ToHtmlString()));

            Assert.Contains("id=\"shell-search-panel\"", html); Assert.Contains("data-shell-search-input", html);
            Assert.Contains("data-shell-search-results", html); Assert.Contains("role=\"listbox\"", html);
            Assert.Matches("<div class=\"shell-search\"[^>]*\\shidden", html);
            Assert.Contains("aria-controls=\"shell-search-panel\"", html); Assert.Contains("aria-expanded=\"false\"", html);
            Assert.Contains(placeholder, html); Assert.DoesNotContain("coming soon", html); Assert.DoesNotContain("netrukus", html);

            // The script is wired after the first interactive render; drive that step directly.
            var shell = new PortalModuleShell();
            foreach (var (name, value) in new (string, object)[] { ("JS", js), ("Configuration", services.GetRequiredService<IConfiguration>()),
                ("L", services.GetRequiredService<Microsoft.Extensions.Localization.IStringLocalizer<LKvitai.MES.BuildingBlocks.WebUI.Localization.ShellResources>>()) })
                typeof(PortalModuleShell).GetProperty(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(shell, value);
            await (Task)typeof(PortalModuleShell).GetMethod("OnAfterRenderAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(shell, [true])!;

            Assert.StartsWith("./_content/LKvitai.MES.BuildingBlocks.WebUI/js/shell-search.js?v=", Assert.IsType<string>(js.ImportArgs![0]));
            var options = JsonSerializer.SerializeToElement(js.Module.InitArgs![2]);
            Assert.Equal(["/sales/shell/search", "/warehouse/shell/search", "/portal/shell/search", "/shopfloor/shell/search", "/finance/shell/search", "/frontline/shell/search"], options.GetProperty("endpoints").EnumerateArray().Select(x => x.GetString()!).ToArray());
            Assert.Equal(2, options.GetProperty("minLength").GetInt32()); Assert.Equal(250, options.GetProperty("debounceMs").GetInt32());
            Assert.Equal(5000, options.GetProperty("timeoutMs").GetInt32()); Assert.InRange(options.GetProperty("limit").GetInt32(), 5, 8);
            var text = options.GetProperty("text");
            Assert.Equal(empty, text.GetProperty("empty").GetString());
            Assert.Equal(ordersHeading, text.GetProperty("kinds").GetProperty("order").GetString());
            Assert.Equal(["order", "customer", "item", "supplier", "location", "document", "screen"], text.GetProperty("kinds").EnumerateObject().Select(p => p.Name).ToArray());
            Assert.Contains("data-shell-search-scopes", html);
            Assert.True(options.TryGetProperty("user", out _));
            foreach (var key in new[] { "hint", "loading", "partial", "keys", "otherKind", "scopeAll", "recentOpened", "recentQueries", "recentClear" }) Assert.False(string.IsNullOrWhiteSpace(text.GetProperty(key).GetString()));

            await shell.DisposeAsync();
            Assert.True(js.Module.Disposed);
        }
        finally { CultureInfo.CurrentUICulture = original; }
    }

    [Fact]
    public async Task Shell_survives_a_missing_search_script()
    {
        var shell = new PortalModuleShell();
        typeof(PortalModuleShell).GetProperty("JS", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(shell, new FailingJs());
        var failure = await Record.ExceptionAsync(async () =>
        {
            await (Task)typeof(PortalModuleShell).GetMethod("OnAfterRenderAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(shell, [true])!;
            await shell.DisposeAsync();
        });
        Assert.Null(failure);
    }

    private static async Task<WebApplication> RelayHostAsync(Transport transport)
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddLocalization();
        builder.Services.AddAuthentication("Cookies").AddCookie("Cookies");
        builder.Services.AddAuthorization();
        builder.Services.AddSalesSessionCredentials();
        builder.Services.AddSingleton<IHttpClientFactory>(new Factory(new HttpClient(transport) { BaseAddress = new Uri("http://sales-api") }));
        var app = builder.Build();
        app.UsePathBase("/sales"); app.UseAuthentication(); app.UseAuthorization();
        app.MapSalesShellSearch();
        await app.StartAsync();
        return app;
    }

    private static void SignIn(WebApplication app, HttpClient client, Guid subject)
    {
        var cookie = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get("Cookies");
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, subject.ToString()), new Claim(ClaimTypes.Name, "Employee")], "Cookies"));
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties
        { IssuedUtc = DateTimeOffset.UtcNow, ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(10) }, "Cookies");
        client.DefaultRequestHeaders.Add("Cookie", cookie.Cookie.Name + "=" + cookie.TicketDataFormat.Protect(ticket));
    }

    private sealed class Orders(params OrderSummaryDto[] rows) : IOrdersQueryService
    {
        public List<OrdersQueryParams> Queries { get; } = [];
        public Task<PagedResult<OrderSummaryDto>> GetOrdersAsync(OrdersQueryParams query, CancellationToken cancellationToken)
        {
            Queries.Add(query);
            return Task.FromResult(new PagedResult<OrderSummaryDto>(rows.Take(query.PageSize).ToList(), rows.Length, query.Page, query.PageSize));
        }
        public Task<OrderDetailsDto?> GetOrderDetailsAsync(string number, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OrdersFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class Factory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) { Assert.Equal(SalesShellSearch.HttpClientName, name); return client; }
    }

    private sealed class Transport : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public List<SearchResultDto> Body { get; set; } = [];
        public List<(string PathAndQuery, string? Bearer)> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri!.PathAndQuery, request.Headers.Authorization?.Parameter));
            return Task.FromResult(new HttpResponseMessage(Status) { Content = JsonContent.Create(Body) });
        }
    }

    private sealed class FixedNavigation : NavigationManager
    {
        public FixedNavigation() => Initialize("http://localhost/sales/", "http://localhost/sales/");
    }

    private sealed class RecordingModule : IJSObjectReference
    {
        public object?[]? InitArgs { get; private set; }
        public bool Disposed { get; private set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (identifier == "init") InitArgs = args;
            return ValueTask.FromResult(default(TValue)!);
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }

    private sealed class RecordingJs : IJSRuntime
    {
        public RecordingModule Module { get; } = new();
        public object?[]? ImportArgs { get; private set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            Assert.Equal("import", identifier); ImportArgs = args;
            return ValueTask.FromResult((TValue)(object)Module);
        }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }

    private sealed class FailingJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new JSException("Failed to fetch dynamically imported module");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
}
