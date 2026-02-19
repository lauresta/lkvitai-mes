using System.Security.Claims;
using FluentAssertions;
using LKvitai.MES.Api.Middleware;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public class SecurityAuditLogIntegrationTests
{
    [Fact]
    [Trait("Category", "AuditLog")]
    public async Task PostRequestThroughMiddleware_ShouldCreateAuditLogEntry()
    {
        await using var db = CreateDbContext();
        var service = new SecurityAuditLogService(db, NullLogger<SecurityAuditLogService>.Instance);

        var middleware = new SecurityAuditLoggingMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/warehouse/v1/items";
        context.Request.Headers.UserAgent = "IntegrationTest";
        context.Response.Body = new MemoryStream();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "user-1")
        ], "Test"));

        await middleware.InvokeAsync(context, service);

        var rows = await service.QueryAsync(new SecurityAuditLogQuery("user-1", "CREATE_ITEM", "ITEM", null, null, 10));

        rows.Should().NotBeEmpty();
        rows.Should().Contain(x => x.Action == "CREATE_ITEM" && x.Resource == "ITEM");
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"security-audit-integration-{Guid.NewGuid():N}")
            .Options;

        return new WarehouseDbContext(options);
    }
}
