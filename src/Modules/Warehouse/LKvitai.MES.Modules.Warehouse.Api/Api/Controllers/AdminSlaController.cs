using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace LKvitai.MES.Modules.Warehouse.Api.Api.Controllers;

[ApiController]
[Route("api/admin/sla")]
[Authorize(Policy = WarehousePolicies.AdminOrAuditor)]
public sealed class AdminSlaController : ControllerBase
{
    private readonly ISlaMonitoringService _slaMonitoringService;

    public AdminSlaController(ISlaMonitoringService slaMonitoringService)
    {
        _slaMonitoringService = slaMonitoringService;
    }

    [HttpPost("report")]
    public async Task<IActionResult> GenerateReport(
        [FromQuery] string month,
        CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(month, "yyyy-MM", out var parsedMonth))
        {
            return BadRequest(new { message = "month must be in yyyy-MM format." });
        }

        var result = await _slaMonitoringService.BuildMonthlyReportAsync(parsedMonth, cancellationToken);
        var bytes = Encoding.UTF8.GetBytes(result.ReportBody);

        return File(bytes, "application/pdf", $"sla-report-{parsedMonth:yyyy-MM}.pdf");
    }
}
