using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Contracts.Quotes;

namespace LKvitai.MES.Modules.Sales.Api.Endpoints;

public static class OrderEntryPricingEndpoints
{
    /// <summary>Read-only list-unit-price lookup; no reservation, total calculation or save approval.</summary>
    public static RouteGroupBuilder MapOrderEntryPricingEndpoints(this RouteGroupBuilder sales)
    {
        sales.MapGet("/order-entry/unit-price", async (
            [AsParameters] UnitPriceRequest request, ISalesUnitPriceLookup lookup,
            HttpContext context, CancellationToken cancellationToken) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            try
            {
                return Results.Ok(await lookup.GetUnitPriceAsync(new SalesUnitPriceQuery(
                    request.TypeId, request.ComponentId, request.WidthMm, request.HeightMm,
                    request.Quantity, request.OrderDate), cancellationToken));
            }
            catch (ArgumentException)
            {
                return Results.Problem(statusCode: 400, title: "Invalid unit-price query",
                    extensions: new Dictionary<string, object?> { ["code"] = "INVALID_UNIT_PRICE_QUERY" });
            }
        });
        return sales;
    }

    public sealed record UnitPriceRequest(int TypeId, int ComponentId, int WidthMm,
        int HeightMm, decimal Quantity, DateOnly OrderDate);
}
