using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LKvitai.MES.BuildingBlocks.PortalAuth;

public static class PortalStructuredBearerAuthenticationDefaults
{
    public const string Scheme = "PortalStructuredBearer";
}

/// <summary>
/// Authenticates the structured token issued by Warehouse local login and stored
/// in the Portal auth ticket as the warehouse_access_token claim.
/// </summary>
public sealed class PortalStructuredBearerAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public PortalStructuredBearerAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var token = authHeader["Bearer ".Length..].Trim();
        if (!PortalStructuredBearerToken.TryParse(token, out var parsed))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid bearer token."));
        }

        if (parsed.IsExpired(DateTimeOffset.UtcNow))
        {
            return Task.FromResult(AuthenticateResult.Fail("Token expired."));
        }

        var identity = new ClaimsIdentity(
            parsed.ToClaims(),
            PortalStructuredBearerAuthenticationDefaults.Scheme,
            ClaimTypes.Name,
            ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, PortalStructuredBearerAuthenticationDefaults.Scheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

}
