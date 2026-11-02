using System.Security.Claims;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;

namespace LKvitai.MES.Modules.Sales.WebUI.Services;

/// <summary>One instance per request/prerender/circuit. Never resolve in a pooled HTTP handler.</summary>
public sealed class SalesSessionCredentials(AuthenticationStateProvider authentication, SalesCredentialProtector protector)
{
    public async Task<Guid?> SubjectAsync()
    {
        var identity=SalesCredentialProtector.PortalIdentity((await authentication.GetAuthenticationStateAsync()).User);
        return SalesCredentialProtector.Subject(identity);
    }
    private readonly object _sync = new();
    private string? _refreshedProof;
    private Guid? _boundSubject;

    public async Task<bool> AcceptProofAsync(string? proof)
    {
        var user = (await authentication.GetAuthenticationStateAsync()).User;
        var identity = SalesCredentialProtector.PortalIdentity(user);
        var subject = SalesCredentialProtector.Subject(identity);
        if (subject is null) return false;
        lock (_sync)
        {
            if (_boundSubject.HasValue && _boundSubject != subject) return false;
            var session = protector.ReadSession(proof);
            if (session is null || session.Subject != subject) return false;
            _boundSubject = subject;
            _refreshedProof = proof;
            return true;
        }
    }

    public async Task<string?> CreateApiCredentialAsync()
    {
        var user = (await authentication.GetAuthenticationStateAsync()).User;
        var identity = SalesCredentialProtector.PortalIdentity(user);
        var subject = SalesCredentialProtector.Subject(identity);
        if (identity is null || subject is null) return null;
        lock (_sync)
        {
            if (_boundSubject.HasValue && _boundSubject != subject) return null;
            _boundSubject = subject;
            var proof = _refreshedProof ?? identity.FindFirst(SalesCredentialProtector.SessionClaim)?.Value;
            var session = protector.ReadSession(proof);
            return session?.Subject == subject ? protector.CreateApi(proof) : null;
        }
    }
}

public static class SalesSessionAuthentication
{
    public static IServiceCollection AddSalesSessionCredentials(this IServiceCollection services, IConfiguration? configuration = null)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<SalesCredentialProtector>();
        services.AddScoped<SalesSessionCredentials>();
        if (configuration?.IsCikadaSsoEnabled() == true)
        {
            services.AddCikadaSsoTokenValidation(configuration);
            services.AddSingleton<CikadaSsoAccountProof>();
            services.AddScoped<CikadaSsoAccountLookup>();
            services.AddHttpClient("CikadaUsers", client =>
            {
                client.BaseAddress = new Uri(configuration["CikadaUsers:BaseUrl"] ?? "http://localhost:5001");
                client.Timeout = TimeSpan.FromSeconds(10);
            }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        }
        services.PostConfigure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            var previous = options.Events.OnValidatePrincipal;
            options.Events.OnValidatePrincipal = async context =>
            {
                if (previous is not null) await previous(context);
                if (context.Principal is null || context.Properties.ExpiresUtc is not { } expires) return;
                var identity = SalesCredentialProtector.PortalIdentity(context.Principal);
                if (identity is null || expires <= DateTimeOffset.UtcNow) return;
                var cloned = new ClaimsIdentity(identity);
                foreach (var claim in cloned.Claims.Where(claim => claim.Type is SalesCredentialProtector.SessionClaim or SalesCredentialProtector.LocalAccountClaim).ToArray())
                    cloned.RemoveClaim(claim);
                context.ReplacePrincipal(new ClaimsPrincipal(cloned));
                if (cloned.FindFirst("auth_source")?.Value == CikadaJwtValidator.AuthSource)
                {
                    var lookup = context.HttpContext.RequestServices.GetService<CikadaSsoAccountLookup>();
                    if (lookup is null) return;
                    try
                    {
                        // Existing SSO cookies predate the explicit issuer/audience metadata.
                        // Upgrade only from their still-valid, signed access token, never a name.
                        if (cloned.FindFirst(CikadaJwtValidator.SsoIssuerClaim) is null)
                        {
                            var token = cloned.FindFirst(PortalOidcDefaults.AccessTokenClaim)?.Value;
                            var validator = context.HttpContext.RequestServices.GetRequiredService<CikadaJwtValidator>();
                            var verified = string.IsNullOrWhiteSpace(token) ? null : await validator.ValidateAsync(
                                token, CookieAuthenticationDefaults.AuthenticationScheme, context.HttpContext.RequestAborted);
                            if (verified is null || verified.FindFirstValue(ClaimTypes.NameIdentifier) != cloned.FindFirst(ClaimTypes.NameIdentifier)?.Value) return;
                            foreach (var type in new[] { CikadaJwtValidator.SsoSubjectClaim, CikadaJwtValidator.SsoUsernameClaim,
                                CikadaJwtValidator.SsoIssuerClaim, CikadaJwtValidator.SsoAudienceClaim })
                            {
                                foreach (var claim in cloned.FindAll(type).ToArray()) cloned.RemoveClaim(claim);
                                foreach (var claim in verified.FindAll(type)) cloned.AddClaim(claim);
                            }
                        }
                        var account = await lookup.ResolveAsync(new ClaimsPrincipal(cloned), context.HttpContext.RequestAborted);
                        if (account is null) return;
                        cloned.AddClaim(new Claim(SalesCredentialProtector.LocalAccountClaim, account.UserId.ToString()));
                    }
                    catch (Exception exception) when (exception is HttpRequestException or System.Text.Json.JsonException or InvalidOperationException
                        || exception is OperationCanceledException && !context.HttpContext.RequestAborted.IsCancellationRequested)
                    {
                        // Keep the Portal session, but issue no Sales credential on lookup failure.
                        return;
                    }
                }
                var subject = SalesCredentialProtector.Subject(cloned);
                if (subject is null) return;
                var codec = context.HttpContext.RequestServices.GetRequiredService<SalesCredentialProtector>();
                cloned.AddClaim(new Claim(SalesCredentialProtector.SessionClaim,
                    codec.CreateSession(subject.Value, cloned.Name ?? subject.ToString()!, expires)));
                context.ReplacePrincipal(new ClaimsPrincipal(cloned));
            };
        });
        return services;
    }

    public static IEndpointConventionBuilder MapSalesSessionProof(this IEndpointRouteBuilder app)
        => app.MapGet("/auth/session-proof", async (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            var fetchSite = context.Request.Headers["Sec-Fetch-Site"].ToString();
            if (fetchSite.Length != 0 && fetchSite != "same-origin") return Results.StatusCode(403);
            if (context.User.HasClaim(SalesCredentialProtector.ReviewClaim, "true")) return Results.StatusCode(403);
            var result = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var identity = result.Succeeded && result.Principal is not null
                ? SalesCredentialProtector.PortalIdentity(result.Principal) : null;
            var proof = identity?.FindFirst(SalesCredentialProtector.SessionClaim)?.Value;
            return proof is null ? Results.StatusCode(401) : Results.Json(new { proof });
        });
}
