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
    public static IServiceCollection AddSalesProtectedAuthentication(this IServiceCollection services, IConfiguration? configuration = null)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<SalesCredentialProtector>();
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, SalesProtectedBearerHandler>(
            SalesCredentialProtector.Scheme, _ => { });
        var schemes = new List<string> { SalesCredentialProtector.Scheme };
        if (configuration?.IsCikadaSsoEnabled() == true)
        {
            services.AddCikadaSsoTokenValidation(configuration);
            services.AddSingleton<CikadaSsoAccountProof>();
            services.AddScoped<CikadaSsoAccountLookup>();
            services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, SalesSsoBearerHandler>(
                SalesCredentialProtector.SsoScheme, _ => { });
            schemes.Add(SalesCredentialProtector.SsoScheme);
        }
        services.AddAuthorization(options => options.AddPolicy(SalesCredentialProtector.Policy,
            new AuthorizationPolicyBuilder(schemes.ToArray()).RequireAuthenticatedUser().Build()));
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


/// <summary>Direct SSO/MCP access accepts signed, issuer/audience-validated JWTs only.
/// Legacy unsigned structured tokens never enter the M2 employee policy.</summary>
public sealed class SalesSsoBearerHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    CikadaJwtValidator validator, CikadaSsoAccountLookup accounts) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string Unavailable = "sales.sso.account-unavailable";
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!AuthenticationHeaderValue.TryParse(Request.Headers.Authorization, out var header)
            || !header.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(header.Parameter) || !CikadaJwtValidator.LooksLikeJwt(header.Parameter))
            return AuthenticateResult.NoResult();
        try
        {
            var principal = await validator.ValidateAsync(header.Parameter, SalesCredentialProtector.SsoScheme, Context.RequestAborted);
            if (principal is null) return AuthenticateResult.Fail("Invalid Sales SSO credential.");
            var account = await accounts.ResolveAsync(principal, Context.RequestAborted);
            if (account is null) return AuthenticateResult.Fail("An active Cikada account is required for Sales.");
            var identity = (ClaimsIdentity)principal.Identity!;
            foreach (var claim in identity.FindAll(SalesCredentialProtector.LocalAccountClaim).ToArray()) identity.RemoveClaim(claim);
            identity.AddClaim(new Claim(SalesCredentialProtector.LocalAccountClaim, account.UserId.ToString()));
            return AuthenticateResult.Success(new AuthenticationTicket(principal, SalesCredentialProtector.SsoScheme));
        }
        catch (Exception exception) when (exception is HttpRequestException or System.Text.Json.JsonException
            or InvalidOperationException || exception is OperationCanceledException && !Context.RequestAborted.IsCancellationRequested)
        {
            Context.Items[Unavailable] = true;
            Logger.LogWarning("Sales SSO account lookup is unavailable.");
            return AuthenticateResult.Fail("The user access service is unavailable.");
        }
    }
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = Context.Items.ContainsKey(Unavailable) ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }
}
