using LKvitai.MES.Modules.Sales.Application.Ports;

namespace LKvitai.MES.Modules.Sales.Api.Endpoints;

/// <summary>Live, employee-only references. Parent Sales group supplies trusted authentication.</summary>
public static class OrderEntryReferencesEndpoints
{
    public static RouteGroupBuilder MapOrderEntryReferenceEndpoints(this RouteGroupBuilder sales)
    {
        var refs = sales.MapGroup("/order-entry/references");
        refs.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); }
            catch (ArgumentException)
            { return Results.Problem(statusCode: 400, title: "Invalid reference query", extensions: new Dictionary<string, object?> { ["code"] = "INVALID_REFERENCE_QUERY" }); }
        });
        refs.MapGet("/series", async (ISalesOrderEntryReferences service, string? search, int? page, int? pageSize, bool? includeHidden, CancellationToken ct)
            => Results.Ok(await service.GetSeriesAsync(search, page ?? 1, pageSize ?? 100, includeHidden ?? false, ct)));
        refs.MapGet("/categories", async (ISalesOrderEntryReferences service, bool? includeInactive, CancellationToken ct)
            => Results.Ok(await service.GetCategoriesAsync(includeInactive ?? false, ct)));
        refs.MapGet("/types", async (ISalesOrderEntryReferences service, int? categoryId, string? search, int? page, int? pageSize, bool? includeInactive, CancellationToken ct)
            => Results.Ok(await service.GetTypesAsync(categoryId, search, page ?? 1, pageSize ?? 100, includeInactive ?? false, ct)));
        refs.MapGet("/types/{typeId:int}", async (ISalesOrderEntryReferences service, int typeId, CancellationToken ct) =>
        {
            var type = await service.GetTypeDefinitionAsync(typeId, ct);
            return type is null ? Results.NotFound() : Results.Ok(type);
        });
        return sales;
    }
}
