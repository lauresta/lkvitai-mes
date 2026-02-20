using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

[Trait("Category", "Correlation")]
public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenHeaderMissing_GeneratesCorrelationId()
    {
        string? observedInPipeline = null;
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            observedInPipeline = CorrelationContext.Current;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        context.Response.Headers.TryGetValue(CorrelationIdMiddleware.HeaderName, out var value).Should().BeTrue();
        Guid.TryParse(value.ToString(), out _).Should().BeTrue();
        observedInPipeline.Should().Be(value.ToString());
        CorrelationContext.Current.Should().BeNull();
    }

    [Fact]
    public async Task InvokeAsync_WhenHeaderProvided_PreservesCorrelationId()
    {
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "client-correlation-123";

        await middleware.InvokeAsync(context);

        context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString()
            .Should().Be("client-correlation-123");
    }
}
