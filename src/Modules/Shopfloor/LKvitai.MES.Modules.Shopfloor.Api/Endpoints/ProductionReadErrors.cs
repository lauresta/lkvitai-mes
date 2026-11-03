namespace LKvitai.MES.Modules.Shopfloor.Api.Endpoints;

internal static class ProductionReadErrors
{
    internal static IResult Access(ProductionQueueAccessException ex) => Results.Problem(ex.Message,
        statusCode: ex.Status, extensions: new Dictionary<string, object?> { ["code"] = ex.Code });

    internal static IResult PermissionDenied() => Results.Problem(
        "Your legacy SQL account does not permit this operation.", statusCode: 403,
        extensions: new Dictionary<string, object?> { ["code"] = "LEGACY_SQL_PERMISSION_DENIED" });
}
