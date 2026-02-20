using LKvitai.MES.Api.Security;
using LKvitai.MES.Modules.Warehouse.Infrastructure.BackgroundJobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Authorize(Policy = WarehousePolicies.AdminOnly)]
[Route("api/admin/idempotency")]
[Route("api/warehouse/v1/admin/idempotency")]
public sealed class IdempotencyController : ControllerBase
{
    private readonly IIdempotencyCleanupService _cleanupService;

    public IdempotencyController(IIdempotencyCleanupService cleanupService)
    {
        _cleanupService = cleanupService;
    }

    [HttpPost("cleanup")]
    public async Task<IActionResult> CleanupAsync(CancellationToken cancellationToken = default)
    {
        var deletedCount = await _cleanupService.CleanupAsync(cancellationToken);
        return Ok(new CleanupResponse(deletedCount, DateTime.UtcNow));
    }

    public sealed record CleanupResponse(int DeletedCount, DateTime ExecutedAtUtc);
}
