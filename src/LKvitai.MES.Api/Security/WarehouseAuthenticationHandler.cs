using System.Security.Claims;
using System.Text.Encodings.Web;
using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace LKvitai.MES.Api.Security;

public static class WarehouseAuthenticationDefaults
{
    public const string Scheme = "WarehouseHeader";
}

/// <summary>
/// Lightweight authentication handler for warehouse APIs.
/// Accepts either:
/// - X-User-Id + X-User-Roles headers
/// - Authorization: Bearer userId|Role1,Role2
/// </summary>
public sealed class WarehouseAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string DevUserId = "dev-user";
    private const string DevRoles =
        $"{WarehouseRoles.Operator}," +
        $"{WarehouseRoles.QCInspector}," +
        $"{WarehouseRoles.WarehouseManager}," +
        $"{WarehouseRoles.WarehouseAdmin}," +
        $"{WarehouseRoles.InventoryAccountant}," +
        $"{WarehouseRoles.CFO}";

    public WarehouseAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers["X-User-Id"].FirstOrDefault();
        var rolesValue = Request.Headers["X-User-Roles"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(userId) &&
            Request.Headers.Authorization.FirstOrDefault() is { } authHeader &&
            authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader["Bearer ".Length..].Trim();
            var segments = token.Split('|', 2, StringSplitOptions.TrimEntries);
            if (segments.Length > 0)
            {
                userId = segments[0];
            }

            if (segments.Length > 1)
            {
                rolesValue = segments[1];
            }
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            var hostEnvironment = Context.RequestServices.GetService<IHostEnvironment>();
            if (hostEnvironment?.IsDevelopment() == true)
            {
                userId = DevUserId;
                rolesValue = DevRoles;
            }
            else
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userId)
        };

        if (!string.IsNullOrWhiteSpace(rolesValue))
        {
            foreach (var role in rolesValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
                claims.Add(new Claim("role", role));
            }
        }

        var identity = new ClaimsIdentity(claims, WarehouseAuthenticationDefaults.Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, WarehouseAuthenticationDefaults.Scheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(
            DomainErrorCodes.Unauthorized,
            "Authentication is required.",
            Context);

        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/problem+json";
        return Response.WriteAsJsonAsync(problemDetails);
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(
            DomainErrorCodes.Forbidden,
            "You do not have permission to access this resource.",
            Context);

        Response.StatusCode = StatusCodes.Status403Forbidden;
        Response.ContentType = "application/problem+json";
        return Response.WriteAsJsonAsync(problemDetails);
    }
}
