using System.Security.Claims;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

/// <summary>Resolves a readable name of the signed-in person for audit text; the opaque subject id is the last resort.</summary>
public static class ActorName
{
    private static readonly string[] NameClaims = ["name", ClaimTypes.Name, "preferred_username", "email", ClaimTypes.Email];

    public static string Resolve(ClaimsPrincipal? user, string fallbackUserId)
    {
        if (user is null)
        {
            return fallbackUserId;
        }

        foreach (var claim in NameClaims)
        {
            var value = user.FindFirstValue(claim);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.IsNullOrWhiteSpace(user.Identity?.Name) ? fallbackUserId : user.Identity.Name.Trim();
    }
}
