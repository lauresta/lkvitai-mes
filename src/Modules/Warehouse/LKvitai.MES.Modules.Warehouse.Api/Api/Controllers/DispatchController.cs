using LKvitai.MES.Api.Security;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/dispatch")]
public sealed class DispatchController : ControllerBase
{
    private readonly WarehouseDbContext _dbContext;

    public DispatchController(WarehouseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("history")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetHistoryAsync(
        [FromQuery] DateTimeOffset? dateFrom,
        [FromQuery] DateTimeOffset? dateTo,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.DispatchHistories.AsNoTracking().AsQueryable();

        if (dateFrom.HasValue)
        {
            query = query.Where(x => x.DispatchedAt >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(x => x.DispatchedAt <= dateTo.Value);
        }

        var rows = await query
            .OrderByDescending(x => x.DispatchedAt)
            .Select(x => new DispatchHistoryResponse(
                x.Id,
                x.ShipmentId,
                x.ShipmentNumber,
                x.OutboundOrderNumber,
                x.Carrier,
                x.TrackingNumber,
                x.VehicleId,
                x.DispatchedAt,
                x.DispatchedBy,
                x.ManualTracking))
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    public sealed record DispatchHistoryResponse(
        Guid Id,
        Guid ShipmentId,
        string ShipmentNumber,
        string OutboundOrderNumber,
        string Carrier,
        string? TrackingNumber,
        string? VehicleId,
        DateTimeOffset DispatchedAt,
        string DispatchedBy,
        bool ManualTracking);
}
