using LKvitai.MES.Modules.Sales.Application.OrderEntry;
using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
namespace LKvitai.MES.Modules.Sales.Api.Endpoints;
public static class OrderEntryPreviewEndpoints
{
    public static RouteGroupBuilder MapOrderEntryPreviewEndpoints(this RouteGroupBuilder sales)
    {
        sales.MapPost("/order-entry/product-preview",async(LegacyProductPreviewRequest request,ILegacyProductPreview preview,HttpContext context,CancellationToken ct)=>
        {
            context.Response.Headers.CacheControl="no-store";
            try{return Results.Ok(await preview.CalculateAsync(request,ct));}
            catch(LegacyProductRuleException ex)
            {return Results.Problem(statusCode:422,title:ex.Message,extensions:new Dictionary<string,object?>{["code"]=ex.Code,["field"]=ex.Field});}
        });
        return sales;
    }
}
