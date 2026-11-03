using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace LKvitai.MES.BuildingBlocks.PortalAuth;

/// <summary>IdP-validated identity metadata, separately bound to the current active Cikada account.</summary>
public sealed record CikadaSsoUserIdentity(string Subject, string Username, string Issuer, string Audience);
public sealed record CikadaSsoAccount(Guid UserId, string Username);

/// <summary>
/// Server-to-server assertion for a validated SSO principal. It cannot be substituted
/// with a caller username, unsigned legacy token or the user-administration proof.
/// </summary>
public sealed class CikadaSsoAccountProof(IDataProtectionProvider provider, TimeProvider clock)
{
    public const string HeaderName = "X-Cikada-Sso-Account-Proof";
    private readonly IDataProtector _protector = provider.CreateProtector("LKvitai.MES.Cikada.SsoAccountLookup.v1");
    private sealed record Assertion(int Version, CikadaSsoUserIdentity Identity, DateTimeOffset IssuedUtc, DateTimeOffset ExpiresUtc, Guid Nonce);

    public string? Create(ClaimsPrincipal principal, CikadaOidcOptions options)
    {
        var user = FromValidatedPrincipal(principal, options, "Cookies", SalesCredentialProtector.SsoScheme,
            PortalStructuredBearerAuthenticationDefaults.Scheme);
        if (user is null) return null;
        var now = clock.GetUtcNow();
        return _protector.Protect(JsonSerializer.Serialize(new Assertion(1, user, now, now.AddMinutes(2), Guid.NewGuid())));
    }

    public CikadaSsoUserIdentity? Read(string? proof, CikadaOidcOptions options)
    {
        if (string.IsNullOrEmpty(proof) || proof.Length > 8192) return null;
        try
        {
            var assertion = JsonSerializer.Deserialize<Assertion>(_protector.Unprotect(proof));
            var now = clock.GetUtcNow();
            return assertion is { Version: 1 } && assertion.Nonce != Guid.Empty
                && assertion.IssuedUtc <= now.AddSeconds(30) && assertion.ExpiresUtc > now
                && assertion.ExpiresUtc > assertion.IssuedUtc
                && assertion.ExpiresUtc - assertion.IssuedUtc <= TimeSpan.FromMinutes(2)
                && ValidIdentity(assertion.Identity, options) ? assertion.Identity : null;
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException or ArgumentException)
        { return null; }
    }

    // Callers must name their real authentication handler; metadata is reconstructed
    // by CikadaJwtValidator, never copied from structured bearer/header inputs.
    public static CikadaSsoUserIdentity? FromValidatedPrincipal(ClaimsPrincipal principal, CikadaOidcOptions options,
        params string[] authenticationTypes)
    {
        var identities = principal.Identities.Where(identity => identity.IsAuthenticated).ToArray();
        if (identities.Length != 1 || !authenticationTypes.Contains(identities[0].AuthenticationType, StringComparer.Ordinal)
            || principal.HasClaim(SalesCredentialProtector.ReviewClaim, "true")) return null;
        var identity = identities[0];
        if (identity.FindFirst("auth_source")?.Value != CikadaJwtValidator.AuthSource) return null;
        var user = new CikadaSsoUserIdentity(
            identity.FindFirst(CikadaJwtValidator.SsoSubjectClaim)?.Value ?? "",
            identity.FindFirst(CikadaJwtValidator.SsoUsernameClaim)?.Value ?? "",
            identity.FindFirst(CikadaJwtValidator.SsoIssuerClaim)?.Value ?? "",
            identity.FindFirst(CikadaJwtValidator.SsoAudienceClaim)?.Value ?? "");
        return user.Subject == identity.FindFirst(ClaimTypes.NameIdentifier)?.Value && ValidIdentity(user, options) ? user : null;
    }

    private static bool ValidIdentity(CikadaSsoUserIdentity? identity, CikadaOidcOptions options)
    {
        if (identity is null || !options.IsConfigured || string.IsNullOrWhiteSpace(identity.Issuer) || string.IsNullOrWhiteSpace(identity.Subject) || identity.Subject.Length > 512
            || string.IsNullOrWhiteSpace(identity.Username) || identity.Username.Length > 100
            || identity.Username != identity.Username.Trim() || identity.Username.Any(char.IsControl)
            || !options.ValidAudiences.Contains(identity.Audience, StringComparer.Ordinal)) return false;
        return new[] { options.Authority }.Concat(options.Issuers)
            .Any(issuer => !string.IsNullOrWhiteSpace(issuer)
                && string.Equals(issuer.TrimEnd('/'), identity.Issuer.TrimEnd('/'), StringComparison.Ordinal));
    }
}

/// <summary>No account creation, role copying, password lookup or SQL mapping from caller input.</summary>
public sealed class CikadaSsoAccountLookup(
    IHttpClientFactory factory, CikadaSsoAccountProof proof, IOptionsMonitor<CikadaOidcOptions> options)
{
    public async Task<CikadaSsoAccount?> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var assertion = proof.Create(principal, options.CurrentValue);
        if (assertion is null) return null;
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/internal/sso-account");
        request.Headers.Add(CikadaSsoAccountProof.HeaderName, assertion);
        using var response = await factory.CreateClient("CikadaUsers").SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden) return null;
        response.EnsureSuccessStatusCode();
        var account = await response.Content.ReadFromJsonAsync<CikadaSsoAccount>(cancellationToken);
        var username = principal.FindFirstValue(CikadaJwtValidator.SsoUsernameClaim);
        return account is { UserId: var id } && id != Guid.Empty
            && string.Equals(account.Username, username, StringComparison.OrdinalIgnoreCase) ? account : null;
    }
}
