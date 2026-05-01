namespace LKvitai.MES.Modules.Sales.Api.Security;

/// <summary>
/// Constants for the Sales dev-only authentication scheme. Mirrors the
/// "*Defaults" pattern used by Microsoft.AspNetCore.Authentication.* and by
/// <c>PortalStructuredBearerAuthenticationDefaults</c> in this repo.
/// </summary>
public static class SalesDevAuthDefaults
{
    /// <summary>Authentication scheme name. Must be unique inside this host.</summary>
    public const string Scheme = "SalesDevAuth";

    /// <summary>Configuration key that enables the dev shim. Off by default.</summary>
    public const string ConfigFlag = "Sales:DevAuthEnabled";

    /// <summary>Synthetic user identifier surfaced as <see cref="System.Security.Claims.ClaimTypes.NameIdentifier"/>.</summary>
    public const string DevUserId = "sales-dev-user";

    /// <summary>Display name surfaced as <see cref="System.Security.Claims.ClaimTypes.Name"/>.</summary>
    public const string DevDisplay = "Sales Dev User";
}
