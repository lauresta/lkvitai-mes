using LKvitai.MES.BuildingBlocks.SharedKernel;
using Serilog.Context;
using System.Diagnostics;

namespace LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;

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
        var traceId = ResolveTraceId(context);
        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        CorrelationContext.Set(correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TraceId", traceId))
        using (LogContext.PushProperty("RequestMethod", context.Request.Method))
        using (LogContext.PushProperty("RequestPath", context.Request.Path.ToString()))
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

    private static string ResolveTraceId(HttpContext context)
    {
        var activityTraceId = Activity.Current?.Id;
        if (!string.IsNullOrWhiteSpace(activityTraceId))
        {
            return activityTraceId;
        }

        return context.TraceIdentifier;
    }
}
