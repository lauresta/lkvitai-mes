using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace LKvitai.MES.Modules.Finance.Api.Security;

/// <summary>Accept only an issuer- and audience-validated Cikada SSO access token.
/// FinanceAccess resolves the active local account and SQL mapping after authentication.</summary>
public sealed class FinanceSsoBearerHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    CikadaJwtValidator validator) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization, out var header) ||
            !header.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(header.Parameter) ||
            !CikadaJwtValidator.LooksLikeJwt(header.Parameter))
            return AuthenticateResult.NoResult();

        var principal = await validator.ValidateAsync(header.Parameter,
            SalesCredentialProtector.SsoScheme, Context.RequestAborted);
        return principal is null
            ? AuthenticateResult.Fail("Invalid Finance SSO credential.")
            : AuthenticateResult.Success(new AuthenticationTicket(principal, SalesCredentialProtector.SsoScheme));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }
}
