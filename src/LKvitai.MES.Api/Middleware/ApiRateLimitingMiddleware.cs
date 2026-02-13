using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;

namespace LKvitai.MES.Api.Middleware;

public sealed class ApiRateLimitingMiddleware
{
    private const int LimitPerMinute = 100;
    private const int WindowSeconds = 60;

    private readonly RequestDelegate _next;
    private readonly ConcurrentDictionary<string, CounterState> _counters = new(StringComparer.Ordinal);

    public ApiRateLimitingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (ShouldBypass(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var key = ResolveKey(context);
        var limit = ResolveLimit(context);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var windowStart = now - (now % WindowSeconds);
        var counter = _counters.GetOrAdd(key, _ => new CounterState(windowStart));

        int used;
        long resetAt;

        lock (counter.Sync)
        {
            if (counter.WindowStartUnix != windowStart)
            {
                counter.WindowStartUnix = windowStart;
                counter.Used = 0;
            }

            counter.Used++;
            used = counter.Used;
            resetAt = counter.WindowStartUnix + WindowSeconds;
        }

        var remaining = Math.Max(0, limit - used);

        context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = remaining.ToString();
        context.Response.Headers["X-RateLimit-Reset"] = resetAt.ToString();

        if (used <= limit)
        {
            await _next(context);
            return;
        }

        var retryAfter = Math.Max(1, resetAt - now);
        context.Response.Headers.RetryAfter = retryAfter.ToString();
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.ContentType = "application/problem+json";

        var body = new
        {
            type = "https://httpstatuses.com/429",
            title = "Too many requests",
            status = 429,
            detail = "API rate limit exceeded. Please retry later.",
            traceId = context.TraceIdentifier,
            errorCode = "RATE_LIMIT_EXCEEDED"
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(body));
    }

    private static bool ShouldBypass(PathString path)
    {
        if (path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/hangfire", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string ResolveKey(HttpContext context)
    {
        var apiKeyId = context.User.FindFirstValue("api_key_id");
        if (!string.IsNullOrWhiteSpace(apiKeyId))
        {
            return $"api-key:{apiKeyId}";
        }

        var userId = context.User.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            return $"user:{userId}";
        }

        var headerId = context.Request.Headers["X-User-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(headerId))
        {
            return $"header:{headerId}";
        }

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ip:{ip}";
    }

    private static int ResolveLimit(HttpContext context)
    {
        var value = context.User.FindFirstValue("api_key_rate_limit");
        return int.TryParse(value, out var parsed) && parsed > 0
            ? parsed
            : LimitPerMinute;
    }

    private sealed class CounterState
    {
        public CounterState(long windowStartUnix)
        {
            WindowStartUnix = windowStartUnix;
        }

        public object Sync { get; } = new();
        public long WindowStartUnix { get; set; }
        public int Used { get; set; }
    }
}
