using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LKvitai.MES.BuildingBlocks.PortalAuth;

public static class PortalStructuredBearerAuthenticationDefaults
{
    public const string Scheme = "PortalStructuredBearer";
}

/// <summary>
/// Authenticates the API bearer forwarded by the WebUIs (warehouse_access_token claim):
/// an SSO JWT validated by <see cref="CikadaJwtValidator"/>, or the legacy structured
/// token issued by Warehouse local login (until Oidc:DisableLegacyTokens is on).
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

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = authHeader["Bearer ".Length..].Trim();
        var sso = Context.RequestServices.GetService<CikadaJwtValidator>();
        if (CikadaJwtValidator.LooksLikeJwt(token))
        {
            if (sso is null || !sso.IsEnabled)
            {
                return AuthenticateResult.Fail("SSO tokens are not accepted by this host.");
            }

            var ssoPrincipal = await sso.ValidateAsync(
                token,
                PortalStructuredBearerAuthenticationDefaults.Scheme,
                Context.RequestAborted);
            return ssoPrincipal is null
                ? AuthenticateResult.Fail("Invalid SSO token.")
                : AuthenticateResult.Success(new AuthenticationTicket(ssoPrincipal, PortalStructuredBearerAuthenticationDefaults.Scheme));
        }

        if (sso?.LegacyTokensDisabled == true)
        {
            return AuthenticateResult.Fail("Legacy bearer tokens are disabled.");
        }

        if (!PortalStructuredBearerToken.TryParse(token, out var parsed))
        {
            return AuthenticateResult.Fail("Invalid bearer token.");
        }

        if (parsed.IsExpired(DateTimeOffset.UtcNow))
        {
            return AuthenticateResult.Fail("Token expired.");
        }

        var identity = new ClaimsIdentity(
            parsed.ToClaims(),
            PortalStructuredBearerAuthenticationDefaults.Scheme,
            ClaimTypes.Name,
            ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, PortalStructuredBearerAuthenticationDefaults.Scheme);
        return AuthenticateResult.Success(ticket);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

}
