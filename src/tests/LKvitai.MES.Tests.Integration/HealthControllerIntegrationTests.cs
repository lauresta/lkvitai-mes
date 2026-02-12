using System.Net;
using LKvitai.MES.Api.Controllers;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Application.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public class HealthControllerIntegrationTests
{
    [Fact]
    public async Task HealthEndpoint_WhenProjectionLagHealthy_Returns200()
    {
        using var server = BuildServer(new ProjectionHealthSnapshot(
            "Healthy",
            "Healthy",
            "Healthy",
            "Healthy",
            DateTimeOffset.UtcNow,
            new Dictionary<string, ProjectionHealthItem>
            {
                ["AvailableStockProjection"] = new(
                    "AvailableStockProjection",
                    10,
                    10,
                    0,
                    0.2,
                    DateTimeOffset.UtcNow,
                    "Healthy")
            }));

        var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "operator-1");
        client.DefaultRequestHeaders.Add("X-User-Roles", WarehouseRoles.Operator);

        var response = await client.GetAsync("/api/warehouse/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RootHealthEndpoint_Anonymous_Returns200()
    {
        using var server = BuildServer(new ProjectionHealthSnapshot(
            "Healthy",
            "Healthy",
            "Healthy",
            "Healthy",
            DateTimeOffset.UtcNow,
            new Dictionary<string, ProjectionHealthItem>()));

        var client = server.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_WhenProjectionLagUnhealthy_Returns503()
    {
        using var server = BuildServer(new ProjectionHealthSnapshot(
            "Degraded",
            "Healthy",
            "Healthy",
            "Unhealthy",
            DateTimeOffset.UtcNow,
            new Dictionary<string, ProjectionHealthItem>
            {
                ["AvailableStockProjection"] = new(
                    "AvailableStockProjection",
                    500,
                    100,
                    400,
                    120,
                    DateTimeOffset.UtcNow.AddMinutes(-2),
                    "Unhealthy")
            }));

        var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "operator-1");
        client.DefaultRequestHeaders.Add("X-User-Roles", WarehouseRoles.Operator);

        var response = await client.GetAsync("/api/warehouse/v1/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private static TestServer BuildServer(ProjectionHealthSnapshot snapshot)
    {
        var webHostBuilder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddControllers()
                    .AddApplicationPart(typeof(HealthController).Assembly);

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

                    options.AddPolicy(WarehousePolicies.OperatorOrAbove, policy =>
                        policy.RequireRole(
                            WarehouseRoles.Operator,
                            WarehouseRoles.QCInspector,
                            WarehouseRoles.WarehouseManager,
                            WarehouseRoles.WarehouseAdmin));
                });

                services.AddSingleton<IProjectionHealthService>(new StubProjectionHealthService(snapshot));
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });

        return new TestServer(webHostBuilder);
    }

    private sealed class StubProjectionHealthService : IProjectionHealthService
    {
        private readonly ProjectionHealthSnapshot _snapshot;

        public StubProjectionHealthService(ProjectionHealthSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public Task<ProjectionHealthSnapshot> GetHealthAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_snapshot);
    }
}
