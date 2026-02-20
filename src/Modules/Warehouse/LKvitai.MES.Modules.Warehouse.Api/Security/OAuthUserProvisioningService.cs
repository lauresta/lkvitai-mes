using System.Security.Claims;

namespace LKvitai.MES.Modules.Warehouse.Api.Security;

public interface IOAuthUserProvisioningService
{
    OAuthProvisioningResult Provision(ClaimsPrincipal principal, IReadOnlyList<string> roles);
}

public sealed record OAuthProvisioningResult(bool IsSuccess, string UserId, IReadOnlyList<string> Roles, string? Error);

public sealed class OAuthUserProvisioningService : IOAuthUserProvisioningService
{
    private readonly IAdminUserStore _adminUserStore;
    private readonly ILogger<OAuthUserProvisioningService> _logger;

    public OAuthUserProvisioningService(
        IAdminUserStore adminUserStore,
        ILogger<OAuthUserProvisioningService> logger)
    {
        _adminUserStore = adminUserStore;
        _logger = logger;
    }

    public OAuthProvisioningResult Provision(ClaimsPrincipal principal, IReadOnlyList<string> roles)
    {
        var username = ResolveUsername(principal);
        if (string.IsNullOrWhiteSpace(username))
        {
            return new OAuthProvisioningResult(false, string.Empty, [], "Unable to resolve username from OAuth claims.");
        }

        var email = ResolveEmail(principal, username);
        var normalizedRoles = NormalizeRoles(roles);

        var existing = _adminUserStore.GetAll()
            .FirstOrDefault(x =>
                string.Equals(x.Username, username, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            if (_adminUserStore.TryUpdate(
                    existing.Id,
                    new UpdateAdminUserRequest(normalizedRoles, "Active", email),
                    out var updated,
                    out var updateError))
            {
                var resultRoles = updated?.Roles ?? normalizedRoles;
                _logger.LogInformation(
                    "OAuth user updated: UserId={UserId}, Username={Username}, Roles={Roles}",
                    existing.Id,
                    existing.Username,
                    string.Join(",", resultRoles));

                return new OAuthProvisioningResult(true, existing.Id.ToString(), resultRoles, null);
            }

            return new OAuthProvisioningResult(false, string.Empty, normalizedRoles, updateError ?? "Failed to update existing OAuth user.");
        }

        var createRequest = new CreateAdminUserRequest(
            username,
            email,
            BuildGeneratedPassword(),
            normalizedRoles,
            "Active");

        if (_adminUserStore.TryCreate(createRequest, out var created, out var createError) && created is not null)
        {
            _logger.LogInformation(
                "OAuth user provisioned: UserId={UserId}, Username={Username}, Roles={Roles}",
                created.Id,
                created.Username,
                string.Join(",", created.Roles));

            return new OAuthProvisioningResult(true, created.Id.ToString(), created.Roles, null);
        }

        return new OAuthProvisioningResult(false, string.Empty, normalizedRoles, createError ?? "Failed to provision OAuth user.");
    }

    private static IReadOnlyList<string> NormalizeRoles(IReadOnlyList<string> roles)
    {
        var normalized = roles
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
        {
            normalized.Add(WarehouseRoles.Operator);
        }

        return normalized;
    }

    private static string ResolveUsername(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue("preferred_username")
               ?? principal.FindFirstValue(ClaimTypes.Name)
               ?? principal.FindFirstValue("name")
               ?? principal.FindFirstValue("upn")
               ?? principal.FindFirstValue(ClaimTypes.Email)
               ?? principal.FindFirstValue("email")
               ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? principal.FindFirstValue("sub")
               ?? string.Empty;
    }

    private static string ResolveEmail(ClaimsPrincipal principal, string username)
    {
        var email = principal.FindFirstValue(ClaimTypes.Email)
                    ?? principal.FindFirstValue("email")
                    ?? principal.FindFirstValue("preferred_username");

        if (!string.IsNullOrWhiteSpace(email) && email.Contains('@', StringComparison.Ordinal))
        {
            return email;
        }

        if (username.Contains('@', StringComparison.Ordinal))
        {
            return username;
        }

        return $"{username.ToLowerInvariant()}@oauth.local";
    }

    private static string BuildGeneratedPassword()
    {
        return $"OAuth!{Guid.NewGuid():N}";
    }
}
