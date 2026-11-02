using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace LKvitai.MES.BuildingBlocks.PortalAuth;

/// <summary>Sales-only cryptographic transport. Contains no user/session cache.</summary>
public sealed class SalesCredentialProtector(IDataProtectionProvider provider, TimeProvider clock)
{
    public const string SessionClaim = "sales_portal_session_proof";
    public const string Scheme = "SalesProtectedBearer";
    public const string Policy = "SalesEmployee";
    public const string ReviewClaim = "sales_review";
    private readonly IDataProtector _session = provider.CreateProtector("LKvitai.MES.Sales.PortalSession.v1");
    private readonly IDataProtector _api = provider.CreateProtector("LKvitai.MES.Sales.ApiCredential.v1");
    public sealed record Payload(int Version, Guid Subject, string Name, string Audience, string Source,
        DateTimeOffset Issued, DateTimeOffset Expires, DateTimeOffset CookieExpires, Guid ProofId);

    public string CreateSession(Guid subject, string name, DateTimeOffset cookieExpires)
    {
        if (subject == Guid.Empty || cookieExpires <= clock.GetUtcNow()) throw new ArgumentException("Invalid session.");
        var now = clock.GetUtcNow();
        var payload = new Payload(1, subject, name, "sales-webui", "portal-cookie", now,
            Min(now.AddMinutes(3), cookieExpires), cookieExpires, Guid.NewGuid());
        return _session.Protect(JsonSerializer.Serialize(payload));
    }

    public Payload? ReadSession(string? proof) => Read(_session, proof, "sales-webui", TimeSpan.FromMinutes(3));
    public Payload? ReadApi(string? token) => Read(_api, token, "sales-api", TimeSpan.FromMinutes(2));

    public string? CreateApi(string? proof)
    {
        var session = ReadSession(proof);
        if (session is null) return null;
        var now = clock.GetUtcNow();
        return _api.Protect(JsonSerializer.Serialize(session with
        {
            Audience = "sales-api", Issued = now, Expires = Min(now.AddMinutes(2), session.Expires)
        }));
    }

    private Payload? Read(IDataProtector protector, string? value, string audience, TimeSpan maxLifetime)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 16000) return null;
        try
        {
            var p = JsonSerializer.Deserialize<Payload>(protector.Unprotect(value));
            var now = clock.GetUtcNow();
            return p is not null && p.Version == 1 && p.Subject != Guid.Empty && p.ProofId != Guid.Empty
                && p.Audience == audience && p.Source == "portal-cookie" && !string.IsNullOrWhiteSpace(p.Name)
                && p.Issued <= now.AddSeconds(30) && p.Expires > now && p.Expires > p.Issued
                && p.Expires - p.Issued <= maxLifetime && p.Expires <= p.CookieExpires ? p : null;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException or ArgumentException)
        { return null; }
    }

    public static ClaimsIdentity? PortalIdentity(ClaimsPrincipal principal)
    {
        if (principal.HasClaim(ReviewClaim, "true")) return null;
        var identities = principal.Identities.Where(x => x.IsAuthenticated).ToArray();
        return identities.Length == 1 && identities[0].AuthenticationType == "Cookies" ? identities[0] : null;
    }
    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
}
