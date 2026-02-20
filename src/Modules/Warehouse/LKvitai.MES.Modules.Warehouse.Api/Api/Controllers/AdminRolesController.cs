using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/admin")]
[Authorize(Policy = WarehousePolicies.AdminOnly)]
public sealed class AdminRolesController : ControllerBase
{
    private readonly IRoleManagementService _roleManagementService;
    private readonly ICurrentUserService _currentUserService;

    public AdminRolesController(
        IRoleManagementService roleManagementService,
        ICurrentUserService currentUserService)
    {
        _roleManagementService = roleManagementService;
        _currentUserService = currentUserService;
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _roleManagementService.GetRolesAsync(cancellationToken);
        return Ok(roles);
    }

    [HttpPost("roles")]
    public async Task<IActionResult> CreateRoleAsync(
        [FromBody] UpsertRolePayload? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        var result = await _roleManagementService.CreateRoleAsync(
            new CreateRoleRequest(request.Name, request.Description, MapPermissions(request.Permissions)),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        return Created($"/api/warehouse/v1/admin/roles/{result.Value.Id}", result.Value);
    }

    [HttpPut("roles/{id:int}")]
    public async Task<IActionResult> UpdateRoleAsync(
        int id,
        [FromBody] UpsertRolePayload? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        var result = await _roleManagementService.UpdateRoleAsync(
            id,
            new UpdateRoleRequest(request.Name, request.Description, MapPermissions(request.Permissions)),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        return Ok(result.Value);
    }

    [HttpDelete("roles/{id:int}")]
    public async Task<IActionResult> DeleteRoleAsync(int id, CancellationToken cancellationToken = default)
    {
        var result = await _roleManagementService.DeleteRoleAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        return NoContent();
    }

    [HttpPost("users/{userId:guid}/roles")]
    public async Task<IActionResult> AssignRoleToUserAsync(
        Guid userId,
        [FromBody] AssignUserRolePayload? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        var result = await _roleManagementService.AssignRoleAsync(
            userId,
            request.RoleId,
            _currentUserService.GetCurrentUserId(),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        return Ok(result.Value);
    }

    private static IReadOnlyList<RolePermissionRequest> MapPermissions(
        IReadOnlyList<RolePermissionPayload>? permissions)
    {
        return permissions?
            .Select(x => new RolePermissionRequest(x.Resource, x.Action, x.Scope ?? "ALL"))
            .ToList() ?? [];
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

    private ObjectResult Failure(Result result)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(result, HttpContext);
        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };
    }

    public sealed record UpsertRolePayload(
        string Name,
        string? Description,
        IReadOnlyList<RolePermissionPayload>? Permissions);

    public sealed record RolePermissionPayload(string Resource, string Action, string? Scope = "ALL");

    public sealed record AssignUserRolePayload(int RoleId);
}
