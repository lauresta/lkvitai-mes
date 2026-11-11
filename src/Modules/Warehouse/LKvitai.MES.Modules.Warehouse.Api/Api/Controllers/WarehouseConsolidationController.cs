using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Projections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

/// <summary>Admin tool that moves stock recorded under old warehouse codes to the configured warehouse.</summary>
[ApiController]
[Authorize(Policy = WarehousePolicies.AdminOnly)]
[Route("api/warehouse/v1/admin/warehouse-consolidation")]
public sealed class WarehouseConsolidationController : ControllerBase
{
    private readonly IWarehouseConsolidationService _service;

    public WarehouseConsolidationController(IWarehouseConsolidationService service)
    {
        _service = service;
    }

    [HttpPost("preview")]
    public async Task<IActionResult> PreviewAsync(CancellationToken cancellationToken = default)
        => Ok(await _service.PreviewAsync(cancellationToken));

    [HttpPost("apply")]
    public async Task<IActionResult> ApplyAsync(
        [FromBody] WarehouseConsolidationApplyRequest? request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 2000)
            return BadRequest(new ProblemDetails
            {
                Status = 400, Title = "Validation failed", Detail = "A reason (1-2000 characters) is required."
            });
        var actor = new ProjectionAuditActor(
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? "unknown",
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            Request.Headers.UserAgent.ToString());
        return Ok(await _service.ApplyAsync(request.Reason, actor, cancellationToken));
    }

    public sealed record WarehouseConsolidationApplyRequest(string Reason);
}
