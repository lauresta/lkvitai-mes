using LKvitai.MES.SharedKernel;
using Serilog.Context;

namespace LKvitai.MES.Api.ErrorHandling;

/// <summary>
/// Propagates correlation id across request, logs, and downstream components.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-ID";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        CorrelationContext.Set(correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }

        CorrelationContext.Clear();
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString().Trim();
        if (!string.IsNullOrWhiteSpace(incoming))
        {
            return incoming;
        }

        return Guid.NewGuid().ToString();
    }
}
