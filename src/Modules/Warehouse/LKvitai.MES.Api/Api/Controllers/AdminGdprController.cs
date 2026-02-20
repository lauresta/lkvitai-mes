using LKvitai.MES.Api.Security;
using LKvitai.MES.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/admin/gdpr")]
public sealed class AdminGdprController : ControllerBase
{
    private readonly IGdprErasureService _service;

    public AdminGdprController(IGdprErasureService service)
    {
        _service = service;
    }

    [HttpPost("erasure-request")]
    [Authorize]
    public async Task<IActionResult> CreateRequestAsync([FromBody] CreateErasureRequestPayload? payload, CancellationToken cancellationToken = default)
    {
        if (payload is null || payload.CustomerId == Guid.Empty || string.IsNullOrWhiteSpace(payload.Reason))
        {
            return BadRequest("customerId and reason are required.");
        }

        try
        {
            var created = await _service.RequestAsync(new CreateErasureRequest(payload.CustomerId, payload.Reason), cancellationToken);
            return Created($"/api/warehouse/v1/admin/gdpr/erasure-request/{created.Id}", created);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
    }

    [HttpGet("erasure-requests")]
    [Authorize(Policy = WarehousePolicies.AdminOnly)]
    public async Task<IActionResult> GetRequestsAsync(CancellationToken cancellationToken = default)
    {
        return Ok(await _service.GetAsync(cancellationToken));
    }

    [HttpPut("erasure-request/{id:guid}/approve")]
    [Authorize(Policy = WarehousePolicies.AdminOnly)]
    public async Task<IActionResult> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _service.ApproveAsync(id, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("erasure-request/{id:guid}/reject")]
    [Authorize(Policy = WarehousePolicies.AdminOnly)]
    public async Task<IActionResult> RejectAsync(Guid id, [FromBody] RejectErasureRequestPayload? payload, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _service.RejectAsync(id, payload?.RejectionReason ?? string.Empty, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    public sealed record CreateErasureRequestPayload(Guid CustomerId, string Reason);

    public sealed record RejectErasureRequestPayload(string? RejectionReason);
}
