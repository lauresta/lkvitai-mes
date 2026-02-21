using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/admin/backups")]
[Authorize(Policy = WarehousePolicies.AdminOnly)]
public sealed class AdminBackupsController : ControllerBase
{
    private readonly IBackupService _backupService;

    public AdminBackupsController(IBackupService backupService)
    {
        _backupService = backupService;
    }

    [HttpPost("trigger")]
    public async Task<IActionResult> TriggerAsync(CancellationToken cancellationToken = default)
    {
        var result = await _backupService.TriggerBackupAsync("MANUAL", cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken = default)
    {
        return Ok(await _backupService.GetHistoryAsync(cancellationToken));
    }

    [HttpPost("restore")]
    public async Task<IActionResult> RestoreAsync([FromBody] RestoreBackupPayload? payload, CancellationToken cancellationToken = default)
    {
        if (payload is null || payload.BackupId == Guid.Empty || string.IsNullOrWhiteSpace(payload.TargetEnvironment))
        {
            return BadRequest("backupId and targetEnvironment are required.");
        }

        var result = await _backupService.RestoreAsync(payload.BackupId, payload.TargetEnvironment, cancellationToken);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    public sealed record RestoreBackupPayload(Guid BackupId, string TargetEnvironment);
}
