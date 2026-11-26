using System.Security.Claims;
using LKvitai.MES.BuildingBlocks.PortalAuth;

namespace LKvitai.MES.Modules.Finance.WebUI.IncomesReport;

/// <summary>
/// Whether the signed-in user may see Finance → Įplaukų judėjimas (page, drawer item, shell search entry).
/// Same list as the API (<c>Finance:Incomes:AllowedUsers</c>, Cikada usernames, fails closed); the API stays the
/// authority and answers 403 to anybody else.
/// </summary>
public static class IncomesAccess
{
    public const string ConfigKey = "Finance:Incomes:AllowedUsers";

    /// <summary>The Cikada username of the Portal cookie: the SSO username, or the local login name.</summary>
    public static string? Username(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true) return null;
        return user.FindFirst("auth_source")?.Value == CikadaJwtValidator.AuthSource
            ? user.FindFirst(CikadaJwtValidator.SsoUsernameClaim)?.Value
            : user.FindFirst(ClaimTypes.Name)?.Value;
    }

    public static bool IsAllowed(ClaimsPrincipal? user, IConfiguration configuration)
    {
        var username = Username(user)?.Trim();
        if (string.IsNullOrEmpty(username)) return false;
        var allowed = configuration.GetSection(ConfigKey).Get<string[]>() ?? [];
        return allowed.Any(value => !string.IsNullOrWhiteSpace(value) &&
                                    string.Equals(value.Trim(), username, StringComparison.OrdinalIgnoreCase));
    }
}
