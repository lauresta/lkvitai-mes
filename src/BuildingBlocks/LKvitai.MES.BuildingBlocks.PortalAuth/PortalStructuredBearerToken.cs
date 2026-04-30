using System.Security.Claims;

namespace LKvitai.MES.BuildingBlocks.PortalAuth;

public sealed record PortalStructuredBearerToken(
    string UserId,
    IReadOnlyList<string> Roles,
    DateTimeOffset? ExpiresAt,
    string? AuthSource,
    bool? MfaVerified)
{
    public static PortalStructuredBearerToken Empty { get; } = new(string.Empty, [], null, null, null);

    public bool IsExpired(DateTimeOffset now) => ExpiresAt.HasValue && now > ExpiresAt.Value;

    public IReadOnlyList<Claim> ToClaims()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, UserId),
            new(ClaimTypes.Name, UserId)
        };

        foreach (var role in Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
            claims.Add(new Claim("role", role));
        }

        if (!string.IsNullOrWhiteSpace(AuthSource))
        {
            claims.Add(new Claim("auth_source", AuthSource));
        }

        if (MfaVerified.HasValue)
        {
            claims.Add(new Claim("mfa_verified", MfaVerified.Value ? "true" : "false"));
        }

        return claims;
    }

    public static bool TryParse(string token, out PortalStructuredBearerToken parsedToken)
    {
        parsedToken = Empty;

        var segments = token.Split('|', StringSplitOptions.TrimEntries);
        if (segments.Length == 0 || string.IsNullOrWhiteSpace(segments[0]))
        {
            return false;
        }

        var roles = Array.Empty<string>();
        if (segments.Length > 1 && !string.IsNullOrWhiteSpace(segments[1]))
        {
            roles = segments[1]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        DateTimeOffset? expiresAt = null;
        if (segments.Length > 2 && long.TryParse(segments[2], out var expUnix))
        {
            expiresAt = DateTimeOffset.FromUnixTimeSeconds(expUnix);
        }

        var authSource = segments.Length > 3 && !string.IsNullOrWhiteSpace(segments[3])
            ? segments[3]
            : null;

        bool? mfaVerified = segments.Length > 4
            ? string.Equals(segments[4], "mfa", StringComparison.OrdinalIgnoreCase)
            : null;

        parsedToken = new PortalStructuredBearerToken(segments[0], roles, expiresAt, authSource, mfaVerified);
        return true;
    }
}
