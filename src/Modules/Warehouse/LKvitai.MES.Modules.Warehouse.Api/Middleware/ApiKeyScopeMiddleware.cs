using System.Security.Claims;
using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Api.Middleware;

public sealed class ApiKeyScopeMiddleware
{
    private readonly RequestDelegate _next;

    public ApiKeyScopeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var principal = context.User;
        var authSource = principal.FindFirstValue("auth_source");

        if (!string.Equals(authSource, "api_key", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var requiredScope = ResolveRequiredScope(context.Request.Method, context.Request.Path);
        if (string.IsNullOrWhiteSpace(requiredScope))
        {
            await _next(context);
            return;
        }

        var hasScope = principal.Claims
            .Where(x => string.Equals(x.Type, "api_scope", StringComparison.OrdinalIgnoreCase))
            .Any(x => string.Equals(x.Value, requiredScope, StringComparison.OrdinalIgnoreCase));

        if (hasScope)
        {
            await _next(context);
            return;
        }

        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(
            DomainErrorCodes.Forbidden,
            $"Insufficient scope: {requiredScope} required",
            context);

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problemDetails);
    }

    private static string? ResolveRequiredScope(string method, PathString path)
    {
        if (HttpMethods.IsGet(method) && path.StartsWithSegments("/api/warehouse/v1/items", StringComparison.OrdinalIgnoreCase))
        {
            return "read:items";
        }

        if ((HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method)) &&
            (path.StartsWithSegments("/api/warehouse/v1/orders", StringComparison.OrdinalIgnoreCase) ||
             path.StartsWithSegments("/api/warehouse/v1/sales-orders", StringComparison.OrdinalIgnoreCase)))
        {
            return "write:orders";
        }

        if (HttpMethods.IsGet(method) && path.StartsWithSegments("/api/warehouse/v1/stock", StringComparison.OrdinalIgnoreCase))
        {
            return "read:stock";
        }

        return null;
    }
}
