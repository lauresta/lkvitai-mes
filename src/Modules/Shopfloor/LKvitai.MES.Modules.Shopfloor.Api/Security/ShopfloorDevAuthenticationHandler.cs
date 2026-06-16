using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace LKvitai.MES.Modules.Shopfloor.Api.Security;

/// <summary>
/// Shopfloor dev-only authentication handler. Only succeeds in
/// Development/Test when <c>Shopfloor:DevAuthEnabled = true</c>; otherwise
/// returns <see cref="AuthenticateResult.NoResult"/>. See the Sales module's
/// <c>SalesDevAuthenticationHandler</c> for the rationale on using a scheme
/// instead of middleware.
/// </summary>
public sealed class ShopfloorDevAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IHostEnvironment _environment;

    public ShopfloorDevAuthenticationHandler(
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
        if (!configuration.GetValue<bool>(ShopfloorDevAuthDefaults.ConfigFlag))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, ShopfloorDevAuthDefaults.DevUserId),
            new Claim(ClaimTypes.Name, ShopfloorDevAuthDefaults.DevDisplay),
        };

        var identity = new ClaimsIdentity(claims, ShopfloorDevAuthDefaults.Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, ShopfloorDevAuthDefaults.Scheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
