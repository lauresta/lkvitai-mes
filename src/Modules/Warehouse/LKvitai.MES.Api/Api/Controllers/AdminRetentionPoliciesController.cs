using LKvitai.MES.Api.Security;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/admin/retention-policies")]
[Authorize(Policy = WarehousePolicies.AdminOnly)]
public sealed class AdminRetentionPoliciesController : ControllerBase
{
    private readonly IRetentionPolicyService _service;

    public AdminRetentionPoliciesController(IRetentionPolicyService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken = default)
    {
        return Ok(await _service.GetAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] UpsertRetentionPolicyPayload? payload, CancellationToken cancellationToken = default)
    {
        if (payload is null)
        {
            return BadRequest("Request body is required.");
        }

        try
        {
            var created = await _service.CreateAsync(
                new CreateRetentionPolicyRequest(
                    ParseDataType(payload.DataType),
                    payload.RetentionPeriodDays,
                    payload.ArchiveAfterDays,
                    payload.DeleteAfterDays,
                    payload.Active),
                cancellationToken);

            return Created($"/api/warehouse/v1/admin/retention-policies/{created.Id}", created);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpsertRetentionPolicyPayload? payload, CancellationToken cancellationToken = default)
    {
        if (payload is null)
        {
            return BadRequest("Request body is required.");
        }

        try
        {
            var updated = await _service.UpdateAsync(
                id,
                new UpdateRetentionPolicyRequest(
                    ParseDataType(payload.DataType),
                    payload.RetentionPeriodDays,
                    payload.ArchiveAfterDays,
                    payload.DeleteAfterDays,
                    payload.Active),
                cancellationToken);

            return updated is null ? NotFound() : Ok(updated);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var deleted = await _service.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("execute")]
    public async Task<IActionResult> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var result = await _service.ExecuteAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPut("legal-hold/{auditLogId:long}")]
    public async Task<IActionResult> SetLegalHoldAsync(long auditLogId, [FromBody] LegalHoldPayload? payload, CancellationToken cancellationToken = default)
    {
        if (payload is null)
        {
            return BadRequest("Request body is required.");
        }

        var updated = await _service.SetAuditLogLegalHoldAsync(auditLogId, payload.LegalHold, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    private static RetentionDataType ParseDataType(string value)
    {
        if (Enum.TryParse<RetentionDataType>(value, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        throw new ArgumentException($"Unsupported dataType '{value}'.", nameof(value));
    }

    public sealed record UpsertRetentionPolicyPayload(
        string DataType,
        int RetentionPeriodDays,
        int? ArchiveAfterDays,
        int? DeleteAfterDays,
        bool Active);

    public sealed record LegalHoldPayload(bool LegalHold);
}
