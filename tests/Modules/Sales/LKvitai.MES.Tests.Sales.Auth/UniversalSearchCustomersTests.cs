using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.RegularExpressions;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.BuildingBlocks.Search;
using LKvitai.MES.Modules.Sales.Api.Endpoints;
using LKvitai.MES.Modules.Sales.Api.Security;
using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Contracts.Common;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using LKvitai.MES.Modules.Sales.Contracts.Orders;
using LKvitai.MES.Modules.Sales.Infrastructure.Sql;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace LKvitai.MES.Tests.Sales.Auth;

/// <summary>
/// Portal "Search everything" (issue #375), second part: the Sales customer search, its SQL contract,
/// and the screens-only relay the remaining module WebUIs map.
/// </summary>
public sealed class UniversalSearchCustomersTests
{
    private static string CustomerSql()
    {
        using var stream = typeof(SqlSalesCustomerSearch).Assembly.GetManifestResourceStream(
            "LKvitai.MES.Modules.Sales.Infrastructure.Sql.Queries.SalesCustomerSearch.sql")!;
        return new StreamReader(stream).ReadToEnd();
    }

    /// <summary>The statement without comments and with LF line ends, whatever the checkout uses.</summary>
    private static string WithoutComments(string sql)
        => Regex.Replace(sql.ReplaceLineEndings("\n"), "--[^\\n]*", string.Empty);

    [Fact]
    public void Customer_query_is_read_only_and_scoped_by_the_callers_branches()
    {
        var sql = WithoutComments(CustomerSql());

        Assert.Contains("FROM dbo.Loc_leidimas() access WHERE access.UKS = us.FilialoID", sql);
        Assert.Contains("ISNULL(c.bHideInApplicationFlag, 0) = 0", sql);
        Assert.Contains("SELECT TOP (@Top)", sql);
        Assert.Contains("LIKE N'%' + @Search + N'%'", sql);
        // Read-only: one SELECT statement over the three legacy tables, nothing else.
        Assert.DoesNotMatch(new Regex(@"\b(INSERT|UPDATE|DELETE|MERGE|EXEC|EXECUTE|ALTER|CREATE|DROP|TRUNCATE|GRANT|INTO)\b", RegexOptions.IgnoreCase), sql);
        var objects = Regex.Matches(sql, @"dbo\.(\w+)").Select(m => m.Groups[1].Value).Distinct().Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["Loc_leidimas", "Uzsakymai", "Zinynas_UzsakymoSerijos", "Zinynas_uzsakovai"], objects);
        Assert.Equal(["@Search", "@Top"], Regex.Matches(sql, @"@\w+").Select(m => m.Value).Distinct().Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Customer_query_does_not_surface_customers_of_privacy_restricted_orders_only()
    {
        var sql = WithoutComments(CustomerSql());

        // The same marker the orders list projects as IsPrivacyRestricted.
        using var paged = typeof(SqlSalesCustomerSearch).Assembly.GetManifestResourceStream(
            "LKvitai.MES.Modules.Sales.Infrastructure.Sql.Queries.SalesOrdersPaged.sql")!;
        Assert.Contains("N'Kliento prasymu anuliuotas'", new StreamReader(paged).ReadToEnd());
        Assert.Contains("ISNULL(LTRIM(RTRIM(u.sNotes)), N'') <> N'Kliento prasymu anuliuotas'", sql);
        Assert.True(LegacyOrderPrivacy.IsRestricted("Kliento prasymu anuliuotas"));
        // The order that makes a customer findable must itself be visible and unrestricted.
        var exists = sql.IndexOf("AND EXISTS (", StringComparison.Ordinal);
        Assert.True(exists > 0 && sql.IndexOf("u.UzsakovoID = c.UzsakovoID", StringComparison.Ordinal) > exists);
    }

    [Fact]
    public async Task Customer_search_resolves_the_actor_before_any_database_access()
    {
        var factory = new RejectingLegacySqlConnectionFactory();
        var search = new SqlSalesCustomerSearch(new SalesSqlOptions { ConnectionString = "Server=must-not-connect.invalid;Database=none" }, factory);

        Assert.Empty(await search.SearchAsync("   ", 5, default));
        Assert.Equal(0, factory.OpenAttempts);

        var error = await Assert.ThrowsAsync<OrderEntryException>(() => search.SearchAsync("sample", 5, default));
        Assert.Equal("LEGACY_MAPPING_REQUIRED", error.Code);
        Assert.Equal(403, error.Status);
        Assert.Equal(1, factory.OpenAttempts);
    }

    [Fact]
    public async Task Customers_come_from_the_customer_search_not_from_the_matched_orders()
    {
        var orders = new Orders(
            new OrderSummaryDto(1, "TS-10-A", new DateOnly(2026, 11, 2), 1m, 0m, false, "Order Only Customer", false, false, false, "S", "entered", "Store", "A"));
        var customers = new Customers(" Sample Buyer ", "sample buyer", "", "Another Sample", "Zeta sample", "X1", "X2", "X3", "X4", "X5");

        var hits = await SearchEndpoints.SearchAsync(orders, "sample", 3, CancellationToken.None, customers);

        Assert.Equal(("sample", 3), Assert.Single(customers.Calls));
        var customerHits = hits.Where(h => h.Kind == SearchKinds.Customer).ToList();
        Assert.Equal(["Sample Buyer", "Another Sample", "Zeta sample"], customerHits.Select(h => h.Title).ToArray());
        Assert.Equal("?search=Sample%20Buyer", customerHits[0].Url);
        Assert.All(customerHits, h => Assert.Null(h.Subtitle));
        Assert.DoesNotContain(hits, h => h.Kind == SearchKinds.Customer && h.Title.StartsWith("Order Only"));
        Assert.Single(hits, h => h.Kind == SearchKinds.Order);

        Assert.Empty(await SearchEndpoints.SearchAsync(orders, "s", 3, CancellationToken.None, customers));
        Assert.Single(customers.Calls);
    }

    [Fact]
    public async Task Endpoint_uses_the_registered_customer_search_under_the_same_policy()
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddSalesProtectedAuthentication();
        var customers = new Customers("Sample Buyer");
        builder.Services.AddSingleton<IOrdersQueryService>(new Orders());
        builder.Services.AddSingleton<ISalesCustomerSearch>(customers);
        await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization();
        app.MapGroup("/api/sales").RequireAuthorization(SalesCredentialProtector.Policy).MapSearchEndpoints();
        await app.StartAsync(); var client = app.GetTestClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/sales/search?q=sample")).StatusCode);
        Assert.Empty(customers.Calls);

        var codec = app.Services.GetRequiredService<SalesCredentialProtector>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            codec.CreateApi(codec.CreateSession(Guid.NewGuid(), "Employee", DateTimeOffset.UtcNow.AddHours(1))));
        var hits = (await client.GetFromJsonAsync<List<SearchResultDto>>("/api/sales/search?q=sample"))!;

        var hit = Assert.Single(hits);
        Assert.Equal(SearchKinds.Customer, hit.Kind); Assert.Equal("Sample Buyer", hit.Title);
    }

    [Fact]
    public async Task Screen_relay_answers_signed_in_portal_users_only_and_keeps_rooted_tile_urls()
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAuthentication("Cookies").AddCookie("Cookies");
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        app.UsePathBase("/portal"); app.UseAuthentication(); app.UseAuthorization();
        app.MapShellScreenSearch(_ => Task.FromResult<IReadOnlyList<ScreenEntry>>(
        [
            new("cms/tiles", "Tiles", "Portal CMS", ["Tiles", "Plytelės"]),
            new("/warehouse/", "Warehouse", "Portal · Operations", ["Warehouse", "Sandėlis"], Rooted: true),
            new("https://elsewhere.example/", "Sandbox elsewhere", null, ["Sandbox elsewhere"], Rooted: true),
        ]));
        await app.StartAsync(); var client = app.GetTestClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/portal/shell/search?q=sand")).StatusCode);

        var cookie = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get("Cookies");
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "Cookies")),
            new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(10) }, "Cookies");
        client.DefaultRequestHeaders.Add("Cookie", cookie.Cookie.Name + "=" + cookie.TicketDataFormat.Protect(ticket));

        var foreign = new HttpRequestMessage(HttpMethod.Get, "/portal/shell/search?q=sand");
        foreign.Headers.Add("Sec-Fetch-Site", "cross-site");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(foreign)).StatusCode);

        var response = await client.GetAsync("/portal/shell/search?q=sand");
        Assert.True(response.Headers.CacheControl?.NoStore);
        var hits = (await response.Content.ReadFromJsonAsync<List<SearchResultDto>>())!;
        var tile = Assert.Single(hits);
        Assert.Equal(SearchKinds.Screen, tile.Kind); Assert.Equal("/warehouse/", tile.Url); Assert.Equal("Warehouse", tile.Title);

        var page = Assert.Single((await client.GetFromJsonAsync<List<SearchResultDto>>("/portal/shell/search?q=plyteles"))!);
        Assert.Equal("/portal/cms/tiles", page.Url);
        Assert.Equal("[]", await client.GetStringAsync("/portal/shell/search?q=p"));
    }

    private sealed class Orders(params OrderSummaryDto[] rows) : IOrdersQueryService
    {
        public Task<PagedResult<OrderSummaryDto>> GetOrdersAsync(OrdersQueryParams query, CancellationToken cancellationToken)
            => Task.FromResult(new PagedResult<OrderSummaryDto>(rows, rows.Length, query.Page, query.PageSize));
        public Task<OrderDetailsDto?> GetOrderDetailsAsync(string number, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<OrdersFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class Customers(params string[] names) : ISalesCustomerSearch
    {
        public List<(string Search, int Limit)> Calls { get; } = [];
        public Task<IReadOnlyList<string>> SearchAsync(string search, int limit, CancellationToken cancellationToken)
        {
            Calls.Add((search, limit));
            return Task.FromResult<IReadOnlyList<string>>(names);
        }
    }
}
