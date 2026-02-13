namespace LKvitai.MES.Api.Security;

public interface IMfaSessionTokenService
{
    string IssueChallengeToken(string userId, IReadOnlyList<string> roles, int challengeTimeoutMinutes);
    string IssueAccessToken(string userId, IReadOnlyList<string> roles, int sessionTimeoutHours);
    bool TryParseToken(string token, out MfaSessionTokenPayload payload);
}

public sealed record MfaSessionTokenPayload(
    string UserId,
    IReadOnlyList<string> Roles,
    DateTimeOffset ExpiresAt,
    string AuthSource,
    bool MfaVerified);

public sealed class MfaSessionTokenService : IMfaSessionTokenService
{
    public string IssueChallengeToken(string userId, IReadOnlyList<string> roles, int challengeTimeoutMinutes)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(Math.Max(1, challengeTimeoutMinutes));
        return BuildToken(userId, roles, expiresAt, mfaVerified: false);
    }

    public string IssueAccessToken(string userId, IReadOnlyList<string> roles, int sessionTimeoutHours)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(Math.Max(1, sessionTimeoutHours));
        return BuildToken(userId, roles, expiresAt, mfaVerified: true);
    }

    public bool TryParseToken(string token, out MfaSessionTokenPayload payload)
    {
        payload = new MfaSessionTokenPayload(string.Empty, [], DateTimeOffset.MinValue, "", false);

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var segments = token.Split('|', StringSplitOptions.TrimEntries);
        if (segments.Length < 4)
        {
            return false;
        }

        if (!long.TryParse(segments[2], out var expUnix))
        {
            return false;
        }

        var roles = segments[1]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        payload = new MfaSessionTokenPayload(
            segments[0],
            roles,
            DateTimeOffset.FromUnixTimeSeconds(expUnix),
            segments[3],
            segments.Length > 4 && string.Equals(segments[4], "mfa", StringComparison.OrdinalIgnoreCase));

        return true;
    }

    private static string BuildToken(string userId, IReadOnlyList<string> roles, DateTimeOffset expiresAt, bool mfaVerified)
    {
        var normalizedRoles = roles
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var suffix = mfaVerified ? "|mfa" : string.Empty;
        return $"{userId}|{string.Join(',', normalizedRoles)}|{expiresAt.ToUnixTimeSeconds()}|oauth{suffix}";
    }
}
