using LKvitai.MES.Modules.Sales.Application.Ports;
namespace LKvitai.MES.Modules.Sales.Api.Endpoints;
public static class OrderEntryCatalogEndpoints
{
    public static RouteGroupBuilder MapOrderEntryCatalogEndpoints(this RouteGroupBuilder sales)
    {
        sales.MapGet("/order-entry/catalog", async(ILiveOrderEntryCatalog catalog,HttpContext context,CancellationToken ct)=>
        {context.Response.Headers.CacheControl="no-store";return Results.Ok(await catalog.GetAsync(ct));});
        sales.MapGet("/order-entry/catalog/usage", async(ILiveOrderEntryCatalog catalog,int storeId,int days,HttpContext context,CancellationToken ct)=>
        {
            context.Response.Headers.CacheControl="no-store";
            return storeId <= 0 || days is < 1 or > 31
                ? Results.BadRequest()
                : Results.Ok(await catalog.GetUsageAsync(storeId,days,ct));
        });
        sales.MapGet("/order-entry/addresses",async(IOrderEntryAddressLookup lookup,int? parentId,string? search,int? id,HttpContext context,CancellationToken ct)=>
        {
            context.Response.Headers.CacheControl="no-store";
            try{return Results.Ok(await lookup.GetAsync(parentId,search,id,ct));}
            catch(ArgumentException){return Results.Problem(statusCode:400,title:"Invalid address query");}
        });
        return sales;
    }
}
