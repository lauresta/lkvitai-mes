using System.Security.Claims;
using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Microsoft.Extensions.Options;

namespace LKvitai.MES.Modules.Warehouse.Api.Middleware;

public sealed class MfaEnforcementMiddleware
{
    private readonly RequestDelegate _next;

    public MfaEnforcementMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IOptionsMonitor<MfaOptions> optionsMonitor)
    {
        var options = optionsMonitor.CurrentValue;
        if (!options.Enabled)
        {
            await _next(context);
            return;
        }

        if (IsExcludedPath(context.Request.Path))
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
        if (!string.Equals(authSource, "oauth", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var userRoles = user.FindAll(ClaimTypes.Role).Select(x => x.Value).ToList();
        var requiresMfa = userRoles.Any(role =>
            options.RequiredRoles.Any(requiredRole => string.Equals(role, requiredRole, StringComparison.OrdinalIgnoreCase)));

        if (!requiresMfa)
        {
            await _next(context);
            return;
        }

        var mfaVerified = user.FindFirstValue("mfa_verified");
        if (string.Equals(mfaVerified, "true", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(
            DomainErrorCodes.Unauthorized,
            "MFA verification required.",
            context);

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problemDetails);
    }

    private static bool IsExcludedPath(PathString path)
    {
        return path.StartsWithSegments("/api/auth/mfa", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/api/auth/oauth", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/api/auth/dev-token", StringComparison.OrdinalIgnoreCase);
    }
}
