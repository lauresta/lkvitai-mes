namespace LKvitai.MES.Api.Security;

public static class WarehousePolicies
{
    public const string AdminOnly = "Warehouse.AdminOnly";
    public const string ManagerOrAdmin = "Warehouse.ManagerOrAdmin";
    public const string QcOrManager = "Warehouse.QcOrManager";
    public const string OperatorOrAbove = "Warehouse.OperatorOrAbove";
}
