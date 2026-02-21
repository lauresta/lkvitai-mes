using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/admin/reason-codes")]
public sealed class AdminReasonCodesController : ControllerBase
{
    private readonly IReasonCodeService _reasonCodeService;

    public AdminReasonCodesController(IReasonCodeService reasonCodeService)
    {
        _reasonCodeService = reasonCodeService;
    }

    [HttpGet]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> GetAsync(
        [FromQuery] string? category,
        [FromQuery] bool? active,
        CancellationToken cancellationToken = default)
    {
        var result = await _reasonCodeService.GetAsync(category, active, cancellationToken);
        if (!result.IsSuccess)
        {
            return ValidationFailure(result.ErrorDetail ?? result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Policy = WarehousePolicies.AdminOnly)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] UpsertReasonCodePayload? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        var result = await _reasonCodeService.CreateAsync(
            new CreateReasonCodeRequest(
                request.Code,
                request.Name,
                request.Description,
                request.ParentId,
                request.Category,
                request.Active),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        return Created($"/api/warehouse/v1/admin/reason-codes/{result.Value.Id}", result.Value);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = WarehousePolicies.AdminOnly)]
    public async Task<IActionResult> UpdateAsync(
        int id,
        [FromBody] UpsertReasonCodePayload? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        var result = await _reasonCodeService.UpdateAsync(
            id,
            new UpdateReasonCodeRequest(
                request.Code,
                request.Name,
                request.Description,
                request.ParentId,
                request.Category,
                request.Active),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        return Ok(result.Value);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = WarehousePolicies.AdminOnly)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var result = await _reasonCodeService.DeleteAsync(id, cancellationToken);
        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        return NoContent();
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

    public sealed record UpsertReasonCodePayload(
        string Code,
        string Name,
        string? Description,
        int? ParentId,
        string Category,
        bool Active = true);
}
