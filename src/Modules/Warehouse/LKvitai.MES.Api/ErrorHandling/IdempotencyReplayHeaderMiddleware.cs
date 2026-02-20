using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Api.ErrorHandling;

/// <summary>
/// Adds replay marker header when a command is served from idempotency cache.
/// </summary>
public sealed class IdempotencyReplayHeaderMiddleware
{
    private readonly RequestDelegate _next;

    public IdempotencyReplayHeaderMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        IdempotencyExecutionContext.Clear();

        await _next(context);

        if (IdempotencyExecutionContext.ConsumeReplayFlag())
        {
            context.Response.Headers["X-Idempotent-Replay"] = "true";
        }
    }
}
