using System.Security.Claims;
using System.Text.Json;
using LKvitai.MES.Api.Services;

namespace LKvitai.MES.Api.Middleware;

public sealed class SecurityAuditLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityAuditLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ISecurityAuditLogService auditLogService)
    {
        var shouldAudit = ShouldAudit(context.Request.Method, context.Request.Path);

        await _next(context);

        if (!shouldAudit)
        {
            return;
        }

        try
        {
            var resource = ResolveResource(context.Request.Path);
            var action = ResolveAction(context.Request.Method, resource);
            var resourceId = ResolveResourceId(context);
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = context.Request.Headers.UserAgent.ToString();

            var details = JsonSerializer.Serialize(new
            {
                path = context.Request.Path.Value,
                query = context.Request.QueryString.Value,
                method = context.Request.Method,
                status = context.Response.StatusCode,
                traceId = context.TraceIdentifier
            });

            await auditLogService.WriteAsync(new SecurityAuditLogWriteRequest(
                userId,
                action,
                resource,
                resourceId,
                ipAddress,
                userAgent,
                DateTimeOffset.UtcNow,
                details),
                context.RequestAborted);
        }
        catch
        {
            // Intentionally swallow audit failures to avoid impacting primary request flow.
        }
    }

    private static bool ShouldAudit(string method, PathString path)
    {
        if (!(HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method)))
        {
            return false;
        }

        if (path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/hangfire", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveResource(PathString path)
    {
        var value = path.Value ?? string.Empty;
        var segments = value.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length == 0)
        {
            return "SYSTEM";
        }

        var resourceCandidate = segments[^1].ToUpperInvariant();
        if (resourceCandidate.Length > 1 && resourceCandidate.EndsWith('S'))
        {
            resourceCandidate = resourceCandidate[..^1];
        }

        return resourceCandidate;
    }

    private static string ResolveAction(string method, string resource)
    {
        var prefix = HttpMethods.IsPost(method)
            ? "CREATE"
            : HttpMethods.IsDelete(method)
                ? "DELETE"
                : "UPDATE";

        return $"{prefix}_{resource}";
    }

    private static string? ResolveResourceId(HttpContext context)
    {
        if (context.Request.RouteValues.TryGetValue("id", out var idValue) && idValue is not null)
        {
            return idValue.ToString();
        }

        return null;
    }
}
