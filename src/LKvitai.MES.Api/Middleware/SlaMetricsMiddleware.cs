using System.Diagnostics;
using LKvitai.MES.Api.Services;

namespace LKvitai.MES.Api.Middleware;

public sealed class SlaMetricsMiddleware
{
    private readonly RequestDelegate _next;

    public SlaMetricsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context, SlaRequestMetricsStore metricsStore)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            metricsStore.AddRequestDuration(sw.Elapsed.TotalMilliseconds);
        }
    }
}
