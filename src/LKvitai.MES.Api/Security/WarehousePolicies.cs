namespace LKvitai.MES.Api.Security;

public static class WarehousePolicies
{
    public const string AdminOnly = "Warehouse.AdminOnly";
    public const string ManagerOrAdmin = "Warehouse.ManagerOrAdmin";
    public const string QcOrManager = "Warehouse.QcOrManager";
    public const string OperatorOrAbove = "Warehouse.OperatorOrAbove";
    public const string SalesAdminOrManager = "Warehouse.SalesAdminOrManager";
    public const string PackingOperatorOrManager = "Warehouse.PackingOperatorOrManager";
    public const string DispatchClerkOrManager = "Warehouse.DispatchClerkOrManager";
    public const string InventoryAccountantOrManager = "Warehouse.InventoryAccountantOrManager";
    public const string CfoOrAdmin = "Warehouse.CfoOrAdmin";
}
