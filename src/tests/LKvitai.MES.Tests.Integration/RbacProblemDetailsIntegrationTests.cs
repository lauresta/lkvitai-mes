using System.Net;
using System.Text.Json;
using LKvitai.MES.Api.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public class RbacProblemDetailsIntegrationTests
{
    [Fact]
    public async Task Unauthenticated_AdminEndpoint_Returns401ProblemDetailsWithTraceId()
    {
        using var server = BuildServer();
        var client = server.CreateClient();

        var response = await client.GetAsync("/api/warehouse/v1/admin/projections/rebuild-status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"traceId\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Operator_AdminEndpoint_Returns403ProblemDetailsWithTraceId()
    {
        using var server = BuildServer();
        var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "operator-1");
        client.DefaultRequestHeaders.Add("X-User-Roles", WarehouseRoles.Operator);

        var response = await client.GetAsync("/api/warehouse/v1/admin/projections/rebuild-status");

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

    private static TestServer BuildServer()
    {
        var webHostBuilder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
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

                    endpoints.MapGet("/api/warehouse/v1/stock/available", async context =>
                    {
                        await context.Response.WriteAsJsonAsync(new { ok = true });
                    }).RequireAuthorization(WarehousePolicies.OperatorOrAbove);
                });
            });

        return new TestServer(webHostBuilder);
    }
}
