using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Api.Services;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/admin/settings")]
public sealed class AdminSettingsController : ControllerBase
{
    private readonly IWarehouseSettingsService _warehouseSettingsService;

    public AdminSettingsController(IWarehouseSettingsService warehouseSettingsService)
    {
        _warehouseSettingsService = warehouseSettingsService;
    }

    [HttpGet]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _warehouseSettingsService.GetAsync(cancellationToken);
        return Ok(settings);
    }

    [HttpPut]
    [Authorize(Policy = WarehousePolicies.AdminOnly)]
    public async Task<IActionResult> UpdateAsync(
        [FromBody] UpdateSettingsPayload? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        var result = await _warehouseSettingsService.UpdateAsync(
            new UpdateWarehouseSettingsRequest(
                request.CapacityThresholdPercent,
                request.DefaultPickStrategy,
                request.LowStockThreshold,
                request.ReorderPoint,
                request.AutoAllocateOrders),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ValidationFailure(result.ErrorDetail ?? result.Error);
        }

        return Ok(result.Value);
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

    public sealed record UpdateSettingsPayload(
        int CapacityThresholdPercent,
        string DefaultPickStrategy,
        int LowStockThreshold,
        int ReorderPoint,
        bool AutoAllocateOrders);
}
