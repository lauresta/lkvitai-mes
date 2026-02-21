using LKvitai.MES.Modules.Warehouse.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Modules.Warehouse.Api.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/monitoring/v1/alerts")]
public sealed class AlertEscalationController : ControllerBase
{
    private readonly IAlertEscalationService _alertEscalationService;

    public AlertEscalationController(IAlertEscalationService alertEscalationService)
    {
        _alertEscalationService = alertEscalationService;
    }

    [HttpPost("escalation")]
    public async Task<ActionResult<AlertEscalationResult>> Escalate(
        [FromBody] AlertManagerWebhookRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _alertEscalationService.ProcessAsync(request, cancellationToken);
        return Accepted(result);
    }
}
