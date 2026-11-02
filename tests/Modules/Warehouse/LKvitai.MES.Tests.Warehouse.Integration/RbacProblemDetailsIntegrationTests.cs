using System.Net;
using System.Text.Json;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Integration;

public class RbacProblemDetailsIntegrationTests
{
    [Theory]
    [InlineData("/api/warehouse/v1/admin/projections/rebuild-status")]
    [InlineData("/api/warehouse/v1/items/manage")]
    public async Task Unauthenticated_ProtectedEndpoint_Returns401ProblemDetailsWithTraceId(string endpoint)
    {
        using var server = BuildServer();
        var client = server.CreateClient();

        var response = await client.GetAsync(endpoint);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"traceId\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/api/warehouse/v1/admin/projections/rebuild-status", WarehouseRoles.Operator)]
    [InlineData("/api/warehouse/v1/items/manage", WarehouseRoles.QCInspector)]
    public async Task InsufficientRole_ProtectedEndpoint_Returns403ProblemDetailsWithTraceId(string endpoint, string role)
    {
        using var server = BuildServer();
        var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "user-1");
        client.DefaultRequestHeaders.Add("X-User-Roles", role);

        var response = await client.GetAsync(endpoint);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"traceId\"", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(WarehouseRoles.Operator)]
    [InlineData(WarehouseRoles.WarehouseAdmin)]
    public async Task OperatorOrAbove_StockEndpoint_AllowsOperatorAndAdmin(string role)
    {
        using var server = BuildServer();
        var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "user-1");
        client.DefaultRequestHeaders.Add("X-User-Roles", role);

        var response = await client.GetAsync("/api/warehouse/v1/stock/available");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(payload.RootElement.TryGetProperty("ok", out _));
    }

    [Fact]
    public async Task SsoOnly_UnsignedHeadersAndLegacyBearer_Return401()
    {
        using var server = BuildServer(ssoOnly: true);
        var client = server.CreateClient();

        using var headers = new HttpRequestMessage(HttpMethod.Get, "/api/warehouse/v1/stock/available");
        headers.Headers.Add("X-User-Id", "user-1");
        headers.Headers.Add("X-User-Roles", WarehouseRoles.WarehouseAdmin);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(headers)).StatusCode);

        var expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        using var bearer = new HttpRequestMessage(HttpMethod.Get, "/api/warehouse/v1/stock/available");
        bearer.Headers.TryAddWithoutValidation("Authorization", $"Bearer user-1|{WarehouseRoles.WarehouseAdmin}|{expires}|local");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(bearer)).StatusCode);
    }

    private static TestServer BuildServer(bool ssoOnly = false)
    {
        var webHostBuilder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                if (ssoOnly)
                {
                    var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Oidc:Enabled"] = "true",
                        ["Oidc:Authority"] = "https://sso.example.test/application/o/cikada/",
                        ["Oidc:ClientId"] = "cikada-web",
                        ["Oidc:DisableLegacyTokens"] = "true"
                    }).Build();
                    services.AddLogging();
                    services.AddCikadaSsoTokenValidation(configuration);
                }

                services
                    .AddAuthentication(WarehouseAuthenticationDefaults.Scheme)
                    .AddScheme<AuthenticationSchemeOptions, WarehouseAuthenticationHandler>(
                        WarehouseAuthenticationDefaults.Scheme,
                        _ => { });

                services.AddAuthorization(options =>
                {
                    options.DefaultPolicy = new AuthorizationPolicyBuilder(WarehouseAuthenticationDefaults.Scheme)
                        .RequireAuthenticatedUser()
                        .Build();

                    options.AddPolicy(WarehousePolicies.AdminOnly, policy =>
                        policy.RequireRole(WarehouseRoles.WarehouseAdmin));

                    options.AddPolicy(WarehousePolicies.ManagerOrAdmin, policy =>
                        policy.RequireRole(
                            WarehouseRoles.WarehouseManager,
                            WarehouseRoles.WarehouseAdmin));

                    options.AddPolicy(WarehousePolicies.OperatorOrAbove, policy =>
                        policy.RequireRole(
                            WarehouseRoles.Operator,
                            WarehouseRoles.QCInspector,
                            WarehouseRoles.WarehouseManager,
                            WarehouseRoles.WarehouseAdmin));
                });
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();

                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/api/warehouse/v1/admin/projections/rebuild-status", async context =>
                    {
                        await context.Response.WriteAsJsonAsync(new { ok = true });
                    }).RequireAuthorization(WarehousePolicies.AdminOnly);

                    endpoints.MapGet("/api/warehouse/v1/items/manage", async context =>
                    {
                        await context.Response.WriteAsJsonAsync(new { ok = true });
                    }).RequireAuthorization(WarehousePolicies.ManagerOrAdmin);

                    endpoints.MapGet("/api/warehouse/v1/stock/available", async context =>
                    {
                        await context.Response.WriteAsJsonAsync(new { ok = true });
                    }).RequireAuthorization(WarehousePolicies.OperatorOrAbove);
                });
            });

        return new TestServer(webHostBuilder);
    }
}
