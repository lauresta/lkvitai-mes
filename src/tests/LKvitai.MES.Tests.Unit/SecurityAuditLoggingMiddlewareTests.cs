using System.Security.Claims;
using FluentAssertions;
using LKvitai.MES.Api.Middleware;
using LKvitai.MES.Api.Services;
using Moq;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class SecurityAuditLoggingMiddlewareTests
{
    [Fact]
    [Trait("Category", "AuditLog")]
    public async Task InvokeAsync_WhenPostApiRequest_ShouldWriteAuditLog()
    {
        var service = new Mock<ISecurityAuditLogService>(MockBehavior.Strict);
        service.Setup(x => x.WriteAsync(It.IsAny<SecurityAuditLogWriteRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var middleware = new SecurityAuditLoggingMiddleware(_ => Task.CompletedTask);
        var context = BuildContext(HttpMethods.Post, "/api/warehouse/v1/items");

        await middleware.InvokeAsync(context, service.Object);

        service.Verify(x => x.WriteAsync(It.IsAny<SecurityAuditLogWriteRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "AuditLog")]
    public async Task InvokeAsync_WhenGetRequest_ShouldNotWriteAuditLog()
    {
        var service = new Mock<ISecurityAuditLogService>(MockBehavior.Strict);
        var middleware = new SecurityAuditLoggingMiddleware(_ => Task.CompletedTask);
        var context = BuildContext(HttpMethods.Get, "/api/warehouse/v1/items");

        await middleware.InvokeAsync(context, service.Object);

        service.Verify(x => x.WriteAsync(It.IsAny<SecurityAuditLogWriteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "AuditLog")]
    public async Task InvokeAsync_WhenSwaggerPath_ShouldNotWriteAuditLog()
    {
        var service = new Mock<ISecurityAuditLogService>(MockBehavior.Strict);
        var middleware = new SecurityAuditLoggingMiddleware(_ => Task.CompletedTask);
        var context = BuildContext(HttpMethods.Post, "/swagger/index.html");

        await middleware.InvokeAsync(context, service.Object);

        service.Verify(x => x.WriteAsync(It.IsAny<SecurityAuditLogWriteRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "AuditLog")]
    public async Task InvokeAsync_WhenAuditServiceThrows_ShouldSwallowException()
    {
        var service = new Mock<ISecurityAuditLogService>(MockBehavior.Strict);
        service.Setup(x => x.WriteAsync(It.IsAny<SecurityAuditLogWriteRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var middleware = new SecurityAuditLoggingMiddleware(_ => Task.CompletedTask);
        var context = BuildContext(HttpMethods.Post, "/api/warehouse/v1/items");

        var act = async () => await middleware.InvokeAsync(context, service.Object);

        await act.Should().NotThrowAsync();
    }

    private static DefaultHttpContext BuildContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Headers.UserAgent = "UnitTest";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "user-1")
        ], "Test"));
        context.Response.Body = new MemoryStream();
        return context;
    }
}
