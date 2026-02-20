using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/admin")]
[Authorize(Policy = WarehousePolicies.AdminOnly)]
public sealed class AdminPermissionsController : ControllerBase
{
    private readonly IRoleManagementService _roleManagementService;

    public AdminPermissionsController(IRoleManagementService roleManagementService)
    {
        _roleManagementService = roleManagementService;
    }

    [HttpGet("permissions")]
    public async Task<IActionResult> GetPermissionsAsync(CancellationToken cancellationToken = default)
    {
        var permissions = await _roleManagementService.GetPermissionsAsync(cancellationToken);
        return Ok(permissions);
    }

    [HttpPost("permissions/check")]
    public async Task<IActionResult> CheckPermissionAsync(
        [FromBody] CheckPermissionPayload? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        if (request.UserId == Guid.Empty)
        {
            return ValidationFailure("userId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Resource) || string.IsNullOrWhiteSpace(request.Action))
        {
            return ValidationFailure("resource and action are required.");
        }

        var allowed = await _roleManagementService.CheckPermissionAsync(
            request.UserId,
            request.Resource,
            request.Action,
            request.OwnerId,
            cancellationToken);

        return Ok(new PermissionCheckResponse(allowed));
    }

    private ObjectResult ValidationFailure(string detail)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(
            DomainErrorCodes.ValidationError,
            detail,
            HttpContext);

        return new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status400BadRequest
        };
    }

    public sealed record CheckPermissionPayload(
        Guid UserId,
        string Resource,
        string Action,
        Guid? OwnerId);

    public sealed record PermissionCheckResponse(bool Allowed);
}
