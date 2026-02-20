namespace LKvitai.MES.Api.Security;

public sealed class DevAuthOptions
{
    public const string SectionName = "DevAuth";

    public string Username { get; set; } = "admin";
    public string Password { get; set; } = "Admin123!";
    public string UserId { get; set; } = "admin-dev";
    public string Roles { get; set; } = string.Join(",",
        WarehouseRoles.Operator,
        WarehouseRoles.QCInspector,
        WarehouseRoles.WarehouseManager,
        WarehouseRoles.WarehouseAdmin,
        WarehouseRoles.InventoryAccountant,
        WarehouseRoles.CFO,
        WarehouseRoles.SalesAdmin,
        WarehouseRoles.PackingOperator,
        WarehouseRoles.DispatchClerk);

    public int TokenLifetimeHours { get; set; } = 24;
}
