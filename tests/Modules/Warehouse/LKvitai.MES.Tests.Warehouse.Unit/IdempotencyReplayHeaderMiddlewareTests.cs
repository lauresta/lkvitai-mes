using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

[Trait("Category", "Idempotency")]
public sealed class IdempotencyReplayHeaderMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenReplayFlagSet_AddsReplayHeader()
    {
        var middleware = new IdempotencyReplayHeaderMiddleware(_ =>
        {
            IdempotencyExecutionContext.MarkReplay();
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        context.Response.Headers.TryGetValue("X-Idempotent-Replay", out var value).Should().BeTrue();
        value.ToString().Should().Be("true");
    }

    [Fact]
    public async Task InvokeAsync_WhenReplayFlagNotSet_DoesNotAddReplayHeader()
    {
        var middleware = new IdempotencyReplayHeaderMiddleware(_ => Task.CompletedTask);

        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context);

        context.Response.Headers.ContainsKey("X-Idempotent-Replay").Should().BeFalse();
    }
}
