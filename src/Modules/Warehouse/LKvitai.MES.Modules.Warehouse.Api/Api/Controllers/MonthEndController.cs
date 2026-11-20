using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Infrastructure.MonthEnd;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

/// <summary>Blueprint 4.9: month-end statement, bridge to Agnum, acts, close and reopen.</summary>
[ApiController]
[Authorize(Policy = WarehousePolicies.InventoryAccountantOrManager)]
[Route("api/warehouse/v1/month-end")]
public sealed class MonthEndController : ControllerBase
{
    private readonly MonthEndService _service;

    public MonthEndController(MonthEndService service) => _service = service;

    public sealed record ReopenRequest(string? Reason);

    private string User => HttpContext.User.Identity?.Name ?? "unknown";

    private static bool Valid(int year, int month) => year is >= 2000 and <= 2100 && month is >= 1 and <= 12;

    [HttpGet("{year:int}/{month:int}/report")]
    public async Task<IActionResult> ReportAsync(int year, int month, CancellationToken ct)
        => Valid(year, month) ? Ok(await _service.GetReportAsync(year, month, ct)) : BadRequest(new { detail = "Invalid month." });

    [HttpGet("{year:int}/{month:int}/state")]
    public async Task<IActionResult> StateAsync(int year, int month, CancellationToken ct)
        => Valid(year, month) ? Ok(await _service.StateAsync(year, month, ct)) : BadRequest(new { detail = "Invalid month." });

    [HttpPost("{year:int}/{month:int}/close")]
    public Task<IActionResult> CloseAsync(int year, int month, CancellationToken ct)
        => Guard(() => _service.CloseAsync(year, month, User, ct));

    [HttpPost("{year:int}/{month:int}/reopen")]
    public Task<IActionResult> ReopenAsync(int year, int month, [FromBody] ReopenRequest? request, CancellationToken ct)
        => Guard(() => _service.ReopenAsync(year, month, request?.Reason ?? string.Empty, User, ct));

    private async Task<IActionResult> Guard(Func<Task> action)
    {
        try
        {
            await action();
            return NoContent();
        }
        catch (MonthEndException ex)
        {
            return Conflict(new { detail = ex.Message, blockers = ex.Blockers });
        }
    }
}
