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
        var traceParent = ResolveTraceParent(context);
        var traceId = ResolveTraceId(traceParent, context);
        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        CorrelationContext.Set(correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TraceId", traceId))
        using (LogContext.PushProperty("TraceParent", traceParent))
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

    private static string ResolveTraceParent(HttpContext context)
    {
        var activityId = Activity.Current?.Id;
        if (!string.IsNullOrWhiteSpace(activityId))
        {
            return activityId;
        }

        var incomingTraceParent = context.Request.Headers["traceparent"].ToString().Trim();
        if (!string.IsNullOrWhiteSpace(incomingTraceParent))
        {
            return incomingTraceParent;
        }

        return context.TraceIdentifier;
    }

    private static string ResolveTraceId(string traceParent, HttpContext context)
    {
        var activityTraceId = Activity.Current?.TraceId.ToHexString();
        if (!string.IsNullOrWhiteSpace(activityTraceId))
        {
            return activityTraceId;
        }

        var extracted = ExtractTraceIdFromTraceParent(traceParent);
        if (!string.IsNullOrWhiteSpace(extracted))
        {
            return extracted;
        }

        return context.TraceIdentifier;
    }

    private static string? ExtractTraceIdFromTraceParent(string? traceParent)
    {
        if (string.IsNullOrWhiteSpace(traceParent))
        {
            return null;
        }

        var parts = traceParent.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 4 && parts[0].Length == 2 && parts[1].Length == 32)
        {
            return parts[1];
        }

        return null;
    }
}
