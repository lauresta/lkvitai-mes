namespace LKvitai.MES.Modules.Portal.Api;

public static class PortalPolicies
{
    public const string AdminOnly = "Portal.AdminOnly";

    public static readonly string[] AdminRoles =
    [
        "Admin",
        "WarehouseAdmin",
        "PortalAdmin"
    ];
}
