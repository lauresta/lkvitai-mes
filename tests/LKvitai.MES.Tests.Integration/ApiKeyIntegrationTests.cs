using System.Security.Claims;
using FluentAssertions;
using LKvitai.MES.Api.Middleware;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public class ApiKeyIntegrationTests
{
    [Fact]
    [Trait("Category", "ApiKey")]
    public async Task GenerateThenValidateApiKey_ShouldAuthenticate()
    {
        await using var db = CreateDbContext();
        var service = new ApiKeyService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<ApiKeyService>.Instance);

        var created = await service.CreateAsync(
            new CreateApiKeyRequest("ERP Integration", ["read:items", "write:orders"], 100, null),
            "admin");

        var validation = await service.ValidateAsync(created.Value.PlainKey);

        created.IsSuccess.Should().BeTrue();
        validation.IsSuccess.Should().BeTrue();
        validation.Scopes.Should().Contain("read:items");
    }

    [Fact]
    [Trait("Category", "ApiKey")]
    public async Task ApiRateLimitingMiddleware_WhenApiKeyLimitExceeded_ShouldReturn429()
    {
        var middleware = new ApiRateLimitingMiddleware(_ => Task.CompletedTask);
        var principal = BuildApiKeyPrincipal(apiKeyId: 42, scopes: ["read:items"], rateLimitPerMinute: 2);

        var first = CreateContext(principal, HttpMethods.Get, "/api/warehouse/v1/items");
        await middleware.InvokeAsync(first);

        var second = CreateContext(principal, HttpMethods.Get, "/api/warehouse/v1/items");
        await middleware.InvokeAsync(second);

        var third = CreateContext(principal, HttpMethods.Get, "/api/warehouse/v1/items");
        await middleware.InvokeAsync(third);

        first.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        second.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        third.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    [Trait("Category", "ApiKey")]
    public async Task ApiKeyScopeMiddleware_WhenScopeMissing_ShouldReturn403()
    {
        var middleware = new ApiKeyScopeMiddleware(_ => Task.CompletedTask);
        var principal = BuildApiKeyPrincipal(apiKeyId: 15, scopes: ["read:items"], rateLimitPerMinute: 100);

        var context = CreateContext(principal, HttpMethods.Post, "/api/warehouse/v1/orders");
        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"api-key-integration-{Guid.NewGuid():N}")
            .Options;

        return new WarehouseDbContext(options);
    }

    private static HttpContext CreateContext(ClaimsPrincipal principal, string method, string path)
    {
        var context = new DefaultHttpContext();
        context.User = principal;
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static ClaimsPrincipal BuildApiKeyPrincipal(int apiKeyId, IReadOnlyList<string> scopes, int rateLimitPerMinute)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, $"api-key:{apiKeyId}"),
            new(ClaimTypes.Name, $"api-key:{apiKeyId}"),
            new("auth_source", "api_key"),
            new("api_key_id", apiKeyId.ToString()),
            new("api_key_rate_limit", rateLimitPerMinute.ToString())
        };

        claims.AddRange(scopes.Select(scope => new Claim("api_scope", scope)));

        var identity = new ClaimsIdentity(claims, "Test");
        return new ClaimsPrincipal(identity);
    }
}
