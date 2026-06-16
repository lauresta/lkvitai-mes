namespace LKvitai.MES.Modules.Shopfloor.Api.Security;

/// <summary>
/// Constants for the Shopfloor dev-only authentication scheme. Mirrors the
/// Sales module's <c>SalesDevAuthDefaults</c>.
/// </summary>
public static class ShopfloorDevAuthDefaults
{
    public const string Scheme = "ShopfloorDevAuth";

    public const string ConfigFlag = "Shopfloor:DevAuthEnabled";

    public const string DevUserId = "shopfloor-dev-user";

    public const string DevDisplay = "Shopfloor Dev User";
}
