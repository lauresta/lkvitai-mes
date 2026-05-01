using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LKvitai.MES.Modules.Sales.Api.Security;

/// <summary>
/// Sales-only dev authentication handler. Only succeeds when:
///   1. The host is running in <see cref="IHostEnvironment.IsDevelopment"/>
///      or <c>EnvironmentName == "Test"</c>; AND
///   2. The configuration flag <see cref="SalesDevAuthDefaults.ConfigFlag"/>
///      (<c>Sales:DevAuthEnabled</c>) is <c>true</c>.
/// In every other case it returns <see cref="AuthenticateResult.NoResult"/>,
/// which makes the scheme a no-op outside developer machines.
///
/// This is registered as an additional <c>AuthenticationScheme</c> on the
/// host, NOT as a piece of <c>app.Use</c> middleware. That is intentional:
/// <c>RequireAuthorization()</c> with an explicit
/// <see cref="Microsoft.AspNetCore.Authorization.AuthorizationPolicy.AuthenticationSchemes"/>
/// list re-runs <c>AuthenticateAsync</c> against each scheme and replaces
/// <c>HttpContext.User</c> with the merged result, which silently discards
/// any principal a custom middleware would have set in pipeline order.
/// Implementing the dev shim as a real handler ensures the synthetic user
/// participates in the same auth pipeline that <c>PolicyEvaluator</c> uses.
///
/// TODO S-3: replace with role-aware identity once the Sales role model is
/// finalised, then drop this scheme from the default policy.
/// </summary>
public sealed class SalesDevAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IHostEnvironment _environment;

    public SalesDevAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IHostEnvironment environment)
        : base(options, logger, encoder)
    {
        _environment = environment;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!_environment.IsDevelopment()
            && !string.Equals(_environment.EnvironmentName, "Test", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var configuration = Context.RequestServices.GetRequiredService<IConfiguration>();
        if (!configuration.GetValue<bool>(SalesDevAuthDefaults.ConfigFlag))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, SalesDevAuthDefaults.DevUserId),
            new Claim(ClaimTypes.Name,           SalesDevAuthDefaults.DevDisplay),
        };

        var identity  = new ClaimsIdentity(claims, SalesDevAuthDefaults.Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket    = new AuthenticationTicket(principal, SalesDevAuthDefaults.Scheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
