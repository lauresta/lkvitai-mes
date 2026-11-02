using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace LKvitai.MES.Modules.Sales.Api.Security;

public static class SalesProtectedAuthentication
{
    public static IServiceCollection AddSalesProtectedAuthentication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<SalesCredentialProtector>();
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, SalesProtectedBearerHandler>(
            SalesCredentialProtector.Scheme, _ => { });
        services.AddAuthorization(options => options.AddPolicy(SalesCredentialProtector.Policy,
            new AuthorizationPolicyBuilder(SalesCredentialProtector.Scheme).RequireAuthenticatedUser().Build()));
        return services;
    }
}

public sealed class SalesProtectedBearerHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    SalesCredentialProtector protector) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization, out var header)
            || !header.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());
        var payload = protector.ReadApi(header.Parameter);
        if (payload is null) return Task.FromResult(AuthenticateResult.Fail("Invalid Sales credential."));
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, payload.Subject.ToString()),
            new Claim(ClaimTypes.Name, payload.Name),
            new Claim("auth_source", payload.Source)
        }, SalesCredentialProtector.Scheme);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(identity), SalesCredentialProtector.Scheme)));
    }
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    { Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; }
}
