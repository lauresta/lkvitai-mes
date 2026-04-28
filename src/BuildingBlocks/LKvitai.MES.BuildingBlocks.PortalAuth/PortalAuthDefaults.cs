namespace LKvitai.MES.BuildingBlocks.PortalAuth;

/// <summary>
/// Shared identifiers for the LKvitai.MES Portal cookie authentication scheme.
/// Both Portal and Warehouse WebUIs read/write the same cookie so users sign in once.
/// </summary>
public static class PortalAuthDefaults
{
    /// <summary>DataProtection application name shared across all portal hosts.</summary>
    public const string ApplicationName = "LKvitai.MES.PortalAuth";

    /// <summary>Cookie name written by AddCookie() and inspected on every host.</summary>
    public const string CookieName = "LKvitai.MES.Portal";

    public const string LoginPath = "/login.html";
    public const string AccessDeniedPath = "/access-denied";
    public const string LogoutPath = "/auth/logout";

    public const string DataProtectionKeysPathConfigKey = "PortalAuth:DataProtectionKeysPath";
    public const string CookieDomainConfigKey = "PortalAuth:CookieDomain";
    public const string LoginBasePathConfigKey = "PortalAuth:LoginBasePath";

    /// <summary>
    /// Default relative path for DataProtection keys. Resolved against
    /// <see cref="Microsoft.Extensions.Hosting.IHostEnvironment.ContentRootPath"/> so
    /// portal/warehouse hosts running in adjacent project folders share the same key ring.
    /// </summary>
    public const string DefaultDataProtectionKeysRelativePath = "../../../../.data-protection-keys";

    public static readonly TimeSpan CookieLifetime = TimeSpan.FromHours(8);
}
