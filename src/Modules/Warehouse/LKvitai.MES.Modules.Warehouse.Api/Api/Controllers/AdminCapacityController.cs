using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Modules.Warehouse.Api.Api.Controllers;

[ApiController]
[Route("api/admin/capacity")]
[Authorize(Policy = WarehousePolicies.AdminOrAuditor)]
public sealed class AdminCapacityController : ControllerBase
{
    private readonly ICapacityPlanningService _capacityPlanningService;

    public AdminCapacityController(ICapacityPlanningService capacityPlanningService)
    {
        _capacityPlanningService = capacityPlanningService;
    }

    [HttpGet("report")]
    public async Task<ActionResult<CapacityReport>> GetReport(CancellationToken cancellationToken)
    {
        var report = await _capacityPlanningService.BuildReportAsync(cancellationToken);
        return Ok(report);
    }
}
