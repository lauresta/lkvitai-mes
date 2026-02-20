using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/admin/dr")]
[Authorize(Policy = WarehousePolicies.AdminOnly)]
public sealed class AdminDisasterRecoveryController : ControllerBase
{
    private readonly IDisasterRecoveryService _service;

    public AdminDisasterRecoveryController(IDisasterRecoveryService service)
    {
        _service = service;
    }

    [HttpPost("drill")]
    public async Task<IActionResult> TriggerDrillAsync([FromBody] TriggerDrillPayload? payload, CancellationToken cancellationToken = default)
    {
        if (payload is null || string.IsNullOrWhiteSpace(payload.Scenario))
        {
            return BadRequest("scenario is required.");
        }

        if (!TryParseScenario(payload.Scenario, out var scenario))
        {
            return BadRequest("Invalid scenario.");
        }

        var result = await _service.TriggerDrillAsync(scenario, cancellationToken);
        return Ok(result);
    }

    [HttpGet("drills")]
    public async Task<IActionResult> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        return Ok(await _service.GetHistoryAsync(cancellationToken));
    }

    public sealed record TriggerDrillPayload(string Scenario);

    private static bool TryParseScenario(string value, out DisasterScenario scenario)
    {
        var normalized = value.Replace("_", string.Empty, StringComparison.Ordinal).Trim();
        return Enum.TryParse(normalized, true, out scenario);
    }
}
