using System.Security.Claims;

namespace LKvitai.MES.Modules.Warehouse.Api.Security;

public interface IOAuthRoleMapper
{
    IReadOnlyList<string> MapRoles(IEnumerable<Claim> claims, OAuthOptions options);
}

public sealed class OAuthRoleMapper : IOAuthRoleMapper
{
    private static readonly Dictionary<string, string> RoleAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Admin"] = WarehouseRoles.WarehouseAdmin,
        ["Manager"] = WarehouseRoles.WarehouseManager
    };

    public IReadOnlyList<string> MapRoles(IEnumerable<Claim> claims, OAuthOptions options)
    {
        var mapped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var claimList = claims.ToList();

        var claimValues = claimList
            .Where(x =>
                string.Equals(x.Type, options.RoleClaimType, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Type, "groups", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Type, "role", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Type, "roles", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList();

        foreach (var claimValue in claimValues)
        {
            if (options.RoleMappings.TryGetValue(claimValue, out var configuredRole) && !string.IsNullOrWhiteSpace(configuredRole))
            {
                AddRole(mapped, configuredRole.Trim());
                continue;
            }

            AddRole(mapped, claimValue);
        }

        if (mapped.Count == 0 && !string.IsNullOrWhiteSpace(options.DefaultRole))
        {
            AddRole(mapped, options.DefaultRole.Trim());
        }

        return mapped.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void AddRole(ISet<string> mapped, string role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return;
        }

        mapped.Add(role);

        if (RoleAliases.TryGetValue(role, out var alias))
        {
            mapped.Add(alias);
        }
    }
}
