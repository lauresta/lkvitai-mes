using LKvitai.MES.Modules.Warehouse.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[AttributeUsage(AttributeTargets.Class)]
public sealed class WarehouseOperationValidationAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is not WarehouseOperationValidationException error) return;
        context.Result = new BadRequestObjectResult(new ProblemDetails { Status = 400, Title = "Validation failed", Detail = error.Message });
        context.ExceptionHandled = true;
    }
}
