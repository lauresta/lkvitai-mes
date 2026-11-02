using System.Security.Claims;
using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using Microsoft.Data.SqlClient;
namespace LKvitai.MES.Modules.Sales.Api.Endpoints;

public static class OrderEntryEndpoints
{
    public static RouteGroupBuilder MapOrderEntryEndpoints(this RouteGroupBuilder sales)
    {
        var group=sales.MapGroup("/order-entry");
        group.AddEndpointFilter(async(context,next)=>
        {
            context.HttpContext.Response.Headers.CacheControl="no-store";
            try{return await next(context);}
            catch(OrderEntryException ex){return Results.Problem(statusCode:ex.Status,title:ex.Message,extensions:new Dictionary<string,object?>{{"code",ex.Code}});}
            catch(SqlException ex)when(ex.Number is 229 or 230)
            {return Results.Problem(statusCode:403,title:"Your legacy SQL account does not permit this operation.",extensions:new Dictionary<string,object?>{{"code","LEGACY_SQL_PERMISSION_DENIED"}});}
            catch(SqlException ex)when(ex.Number==1205){return Results.Problem(statusCode:409,title:"Concurrent database operation was rolled back. Retry the same command.",extensions:new Dictionary<string,object?>{{"code","RETRY_SAME_COMMAND"}});}
            catch(SqlException ex)when(ex.Number>=50000){return Results.Problem(statusCode:422,title:ex.Message,extensions:new Dictionary<string,object?>{{"code","LEGACY_VALIDATION"}});}
            catch(SqlException ex)
            {
                // Keep SQL text, parameter values and connection details out of the
                // response/log. Numeric diagnostics identify legacy schema failures.
                context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Sales.OrderEntry")
                    .LogError("Legacy SQL operation failed: number {SqlNumber}, state {SqlState}, class {SqlClass}, procedure {SqlProcedure}, line {SqlLine}, connection {SqlConnectionId}, inner type {InnerType}, native code {NativeCode}, trace {TraceId}",
                        ex.Number,ex.State,ex.Class,ex.Procedure,ex.LineNumber,ex.ClientConnectionId,
                        ex.InnerException?.GetType().Name,(ex.InnerException as System.ComponentModel.Win32Exception)?.NativeErrorCode,
                        context.HttpContext.TraceIdentifier);
                var message=HttpMethods.IsGet(context.HttpContext.Request.Method)
                    ? "The database could not load the requested data. Try again."
                    : "The database did not confirm the save. Your input is retained; use Check / retry save.";
                return Results.Problem(statusCode:503,title:message,extensions:new Dictionary<string,object?>{{"code","DATABASE_OUTCOME_UNCONFIRMED"}});
            }
        });
        group.MapGet("/orders/{id:long}",async(long id,ISalesOrderEntryService service,CancellationToken ct)=>
        {var order=await service.GetAsync(id,ct);return order is null?Results.NotFound():Results.Ok(order);});
        group.MapGet("/commands/{id:guid}",async(Guid id,ClaimsPrincipal user,ISalesOrderEntryService service,CancellationToken ct)=>
        {var outcome=await service.GetOutcomeAsync(Actor(user),id,ct);return outcome is null?Results.NotFound():Results.Ok(outcome);});
        group.MapPost("/orders",async(CreateOrderCommand command,ClaimsPrincipal user,ISalesOrderEntryService service,CancellationToken ct)=>Results.Ok(await service.CreateAsync(Actor(user),command,ct)));
        group.MapPut("/orders/{id:long}",async(long id,UpdateOrderCommand command,ClaimsPrincipal user,ISalesOrderEntryService service,CancellationToken ct)=>Results.Ok(await service.UpdateAsync(Actor(user),id,command,ct)));
        group.MapPost("/orders/{id:long}/products",async(long id,CreateProductCommand command,ClaimsPrincipal user,ISalesOrderEntryService service,CancellationToken ct)=>Results.Ok(await service.CreateProductAsync(Actor(user),id,command,ct)));
        group.MapPut("/orders/{id:long}/products/{productId:long}",async(long id,long productId,ConfigureProductCommand command,ClaimsPrincipal user,ISalesOrderEntryService service,CancellationToken ct)=>Results.Ok(await service.ConfigureProductAsync(Actor(user),id,productId,command,ct)));
        group.MapDelete("/orders/{id:long}/products/{productId:long}",async(long id,long productId,[Microsoft.AspNetCore.Mvc.FromBody] DeleteProductCommand command,ClaimsPrincipal user,ISalesOrderEntryService service,CancellationToken ct)=>Results.Ok(await service.DeleteProductAsync(Actor(user),id,productId,command,ct)));
        group.MapPost("/orders/{id:long}/products/{productId:long}/accessories",async(long id,long productId,SaveAccessoryCommand command,ClaimsPrincipal user,ISalesOrderEntryService service,CancellationToken ct)=>Results.Ok(await service.SaveAccessoryAsync(Actor(user),id,productId,command,ct)));
        group.MapDelete("/orders/{id:long}/products/{productId:long}/accessories/{accessoryId:int}",async(long id,long productId,int accessoryId,[Microsoft.AspNetCore.Mvc.FromBody] DeleteAccessoryCommand command,ClaimsPrincipal user,ISalesOrderEntryService service,CancellationToken ct)=>Results.Ok(await service.DeleteAccessoryAsync(Actor(user),id,productId,accessoryId,command,ct)));
        group.MapPut("/orders/{id:long}/address",async(long id,SaveOrderAddressCommand command,ClaimsPrincipal user,ISalesOrderEntryService service,CancellationToken ct)=>Results.Ok(await service.SaveAddressAsync(Actor(user),id,command,ct)));
        group.MapPost("/orders/{id:long}/duties",async(long id,SaveOrderDutyCommand command,ClaimsPrincipal user,ISalesOrderEntryService service,CancellationToken ct)=>Results.Ok(await service.SaveDutyAsync(Actor(user),id,command,ct)));
        group.MapPost("/orders/{id:long}/cancel",async(long id,CancelOrderCommand command,ClaimsPrincipal user,ISalesOrderEntryService service,CancellationToken ct)=>Results.Ok(await service.CancelAsync(Actor(user),id,command,ct)));
        group.MapPut("/orders/{id:long}/final-price",async(long id,SetOrderFinalPriceCommand command,ClaimsPrincipal user,ISalesOrderEntryService service,CancellationToken ct)=>Results.Ok(await service.SetFinalPriceAsync(Actor(user),id,command,ct)));
        group.MapDelete("/orders/{id:long}/duties/{dutyId:int}/{employeeId:int}",async(long id,int dutyId,int employeeId,[Microsoft.AspNetCore.Mvc.FromBody] DeleteOrderDutyCommand command,ClaimsPrincipal user,ISalesOrderEntryService service,CancellationToken ct)=>Results.Ok(await service.DeleteDutyAsync(Actor(user),id,dutyId,employeeId,command,ct)));
        group.MapPost("/orders/{id:long}/products/{productId:long}/duplicate",async(long id,long productId,DuplicateProductCommand command,ClaimsPrincipal user,ISalesOrderEntryService service,CancellationToken ct)=>Results.Ok(await service.DuplicateProductAsync(Actor(user),id,productId,command,ct)));
        group.MapOrderEntryFinancialEndpoints();
        return sales;
    }
    private static Guid Actor(ClaimsPrincipal user)=>Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier),out var actor)&&actor!=Guid.Empty?actor:throw new OrderEntryException("INVALID_ACTOR","Trusted Portal employee identity required.",401);
}
