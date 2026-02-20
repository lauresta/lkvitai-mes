using System.Security.Claims;
using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Middleware;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using Moq;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class PermissionPolicyMiddlewareTests
{
    [Fact]
    [Trait("Category", "Permission")]
    public async Task InvokeAsync_WhenPathNotMapped_ShouldPassThrough()
    {
        var middleware = new PermissionPolicyMiddleware(_ => Task.CompletedTask);
        var roleService = new Mock<IRoleManagementService>(MockBehavior.Strict);

        var context = BuildContext(HttpMethods.Get, "/health", Guid.NewGuid(), authSource: "oauth");
        await middleware.InvokeAsync(context, roleService.Object);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    [Trait("Category", "Permission")]
    public async Task InvokeAsync_WhenNoAssignments_ShouldPassThrough()
    {
        var middleware = new PermissionPolicyMiddleware(_ => Task.CompletedTask);
        var roleService = new Mock<IRoleManagementService>(MockBehavior.Strict);
        roleService.Setup(x => x.HasAnyRoleAssignmentsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var context = BuildContext(HttpMethods.Get, "/api/warehouse/v1/items", Guid.NewGuid(), authSource: "oauth");
        await middleware.InvokeAsync(context, roleService.Object);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    [Trait("Category", "Permission")]
    public async Task InvokeAsync_WhenAssignmentExistsAndPermissionMissing_ShouldReturnForbidden()
    {
        var middleware = new PermissionPolicyMiddleware(_ => Task.CompletedTask);
        var roleService = new Mock<IRoleManagementService>(MockBehavior.Strict);
        roleService.Setup(x => x.HasAnyRoleAssignmentsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        roleService.Setup(x => x.HasPermissionAsync(It.IsAny<Guid>(), "ITEM", "READ", "ALL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var context = BuildContext(HttpMethods.Get, "/api/warehouse/v1/items", Guid.NewGuid(), authSource: "oauth");
        await middleware.InvokeAsync(context, roleService.Object);

        context.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    [Trait("Category", "Permission")]
    public async Task InvokeAsync_WhenApiKeyAuth_ShouldBypassPermissionPolicy()
    {
        var middleware = new PermissionPolicyMiddleware(_ => Task.CompletedTask);
        var roleService = new Mock<IRoleManagementService>(MockBehavior.Strict);

        var context = BuildContext(HttpMethods.Get, "/api/warehouse/v1/items", Guid.NewGuid(), authSource: "api_key");
        await middleware.InvokeAsync(context, roleService.Object);

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    private static HttpContext BuildContext(string method, string path, Guid userId, string authSource)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("auth_source", authSource)
        };

        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        return context;
    }
}
