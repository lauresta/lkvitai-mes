using LKvitai.MES.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Api.Api.Controllers;

[ApiController]
[Route("api/test")]
[AllowAnonymous]
public sealed class CapacitySimulationController : ControllerBase
{
    private readonly ICapacityPlanningService _capacityPlanningService;

    public CapacitySimulationController(ICapacityPlanningService capacityPlanningService)
    {
        _capacityPlanningService = capacityPlanningService;
    }

    [HttpPost("simulate-capacity-alert")]
    public ActionResult<CapacityAlertSimulation> Simulate(
        [FromQuery] string type,
        [FromQuery] double utilization)
    {
        var result = _capacityPlanningService.SimulateAlert(type, utilization);
        return Ok(result);
    }
}
