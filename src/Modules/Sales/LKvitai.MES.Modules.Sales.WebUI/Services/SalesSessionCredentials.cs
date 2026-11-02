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
        return Guid.TryParse(identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value,out var subject) && subject!=Guid.Empty ? subject : null;
    }
    private readonly object _sync = new();
    private string? _refreshedProof;
    private Guid? _boundSubject;

    public async Task<bool> AcceptProofAsync(string? proof)
    {
        var user = (await authentication.GetAuthenticationStateAsync()).User;
        var identity = SalesCredentialProtector.PortalIdentity(user);
        if (identity is null || !Guid.TryParse(identity.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var subject))
            return false;
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
        if (identity is null || !Guid.TryParse(identity.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var subject)) return null;
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
    public static IServiceCollection AddSalesSessionCredentials(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<SalesCredentialProtector>();
        services.AddScoped<SalesSessionCredentials>();
        services.PostConfigure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
        {
            var previous = options.Events.OnValidatePrincipal;
            options.Events.OnValidatePrincipal = async context =>
            {
                if (previous is not null) await previous(context);
                if (context.Principal is null || context.Properties.ExpiresUtc is not { } expires) return;
                var identity = SalesCredentialProtector.PortalIdentity(context.Principal);
                if (identity is null || !Guid.TryParse(identity.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var subject)
                    || subject == Guid.Empty || expires <= DateTimeOffset.UtcNow) return;
                var cloned = new ClaimsIdentity(identity);
                foreach (var claim in cloned.FindAll(SalesCredentialProtector.SessionClaim).ToArray()) cloned.RemoveClaim(claim);
                var codec = context.HttpContext.RequestServices.GetRequiredService<SalesCredentialProtector>();
                cloned.AddClaim(new Claim(SalesCredentialProtector.SessionClaim,
                    codec.CreateSession(subject, cloned.Name ?? subject.ToString(), expires)));
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
