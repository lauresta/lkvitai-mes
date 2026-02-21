using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/admin/audit-logs")]
[Authorize(Policy = WarehousePolicies.AdminOnly)]
public sealed class AdminAuditLogsController : ControllerBase
{
    private readonly ISecurityAuditLogService _auditLogService;

    public AdminAuditLogsController(ISecurityAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAsync(
        [FromQuery] string? userId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? resource = null,
        [FromQuery] DateTimeOffset? startDate = null,
        [FromQuery] DateTimeOffset? endDate = null,
        [FromQuery] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await _auditLogService.QueryAsync(
            new SecurityAuditLogQuery(userId, action, resource, startDate, endDate, limit),
            cancellationToken);

        return Ok(rows);
    }
}
