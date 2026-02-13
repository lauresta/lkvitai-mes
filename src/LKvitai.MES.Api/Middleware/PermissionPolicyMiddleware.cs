using System.Security.Claims;
using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Api.Services;
using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Api.Middleware;

public sealed class PermissionPolicyMiddleware
{
    private readonly RequestDelegate _next;

    public PermissionPolicyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IRoleManagementService roleManagementService)
    {
        if (ShouldBypass(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var requirement = ResolveRequirement(context.Request.Method, context.Request.Path);
        if (requirement is null)
        {
            await _next(context);
            return;
        }

        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var authSource = user.FindFirstValue("auth_source");
        if (string.Equals(authSource, "api_key", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await _next(context);
            return;
        }

        var hasAssignments = await roleManagementService.HasAnyRoleAssignmentsAsync(userId, context.RequestAborted);
        if (!hasAssignments)
        {
            await _next(context);
            return;
        }

        var allowed = await roleManagementService.HasPermissionAsync(
            userId,
            requirement.Value.Resource,
            requirement.Value.Action,
            "ALL",
            context.RequestAborted);

        if (allowed)
        {
            await _next(context);
            return;
        }

        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(
            DomainErrorCodes.Forbidden,
            $"Permission required: {requirement.Value.Resource}:{requirement.Value.Action}",
            context);

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problemDetails);
    }

    private static bool ShouldBypass(PathString path)
    {
        return path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/hangfire", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/api/warehouse/v1/admin", StringComparison.OrdinalIgnoreCase);
    }

    private static (string Resource, string Action)? ResolveRequirement(string method, PathString path)
    {
        if (path.StartsWithSegments("/api/warehouse/v1/items", StringComparison.OrdinalIgnoreCase))
        {
            return HttpMethods.IsGet(method) ? ("ITEM", "READ") : ("ITEM", "UPDATE");
        }

        if (path.StartsWithSegments("/api/warehouse/v1/locations", StringComparison.OrdinalIgnoreCase))
        {
            return HttpMethods.IsGet(method) ? ("LOCATION", "READ") : ("LOCATION", "UPDATE");
        }

        if (path.StartsWithSegments("/api/warehouse/v1/qc", StringComparison.OrdinalIgnoreCase))
        {
            return HttpMethods.IsGet(method) ? ("QC", "READ") : ("QC", "UPDATE");
        }

        if (path.StartsWithSegments("/api/warehouse/v1/orders", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/api/warehouse/v1/sales-orders", StringComparison.OrdinalIgnoreCase))
        {
            return HttpMethods.IsGet(method) ? ("ORDER", "READ") : ("ORDER", "UPDATE");
        }

        return null;
    }
}
