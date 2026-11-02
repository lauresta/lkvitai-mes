using System.Security.Claims;
using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
namespace LKvitai.MES.Modules.Sales.Api.Endpoints;
public static class OrderEntryFinancialEndpoints
{
    /// <summary>Mapped on the existing trusted group/filter; each command independently defaults off.</summary>
    public static RouteGroupBuilder MapOrderEntryFinancialEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/orders/{id:long}/payment-references",async(long id,int? methodId,int? bankTransferId,int? registerId,ISalesOrderEntryService service,CancellationToken ct)=>
            Results.Ok(await Financial(service).GetPaymentReferencesAsync(id,ct,new(methodId,bankTransferId,registerId))));
        group.MapGet("/orders/{id:long}/payment-bank-transfers",async(long id,string? search,int? page,int? pageSize,int? selectedId,ISalesOrderEntryService service,CancellationToken ct)=>
            Results.Ok(await Financial(service).SearchPaymentBankTransfersAsync(id,search,page??1,pageSize??25,selectedId,ct)));
        group.MapPost("/orders/{id:long}/payments",async(long id,PostOrderPaymentCommand command,ClaimsPrincipal user,ISalesOrderEntryService service,CancellationToken ct)=>
            Results.Ok(await Financial(service).PostPaymentAsync(Actor(user),id,command,ct)));
        group.MapPost("/orders/{id:long}/confirm",async(long id,ConfirmOrderProductsCommand command,ClaimsPrincipal user,ISalesOrderEntryService service,CancellationToken ct)=>
            Results.Ok(await Financial(service).ConfirmProductsAsync(Actor(user),id,command,ct)));
        return group;
    }
    private static ISalesOrderFinancialCommands Financial(ISalesOrderEntryService service)=>service as ISalesOrderFinancialCommands??throw new OrderEntryException("FINANCIAL_DISABLED","This deployment has no financial/confirmation adapter.",403);
    private static Guid Actor(ClaimsPrincipal user)=>Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier),out var actor)&&actor!=Guid.Empty?actor:throw new OrderEntryException("INVALID_ACTOR","Trusted Portal employee identity required.",401);
}
