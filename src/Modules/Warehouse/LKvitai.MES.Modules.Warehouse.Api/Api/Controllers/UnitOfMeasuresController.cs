using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/unit-of-measures")]
public sealed class UnitOfMeasuresController : ControllerBase
{
    private readonly WarehouseDbContext _dbContext;

    public UnitOfMeasuresController(WarehouseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetAsync(
        [FromQuery] string? type,
        [FromQuery] string? search,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.UnitOfMeasures
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(type))
        {
            var normalizedType = type.Trim();
            query = query.Where(x => x.Type == normalizedType);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Code.ToLower().Contains(normalized) ||
                x.Name.ToLower().Contains(normalized));
        }

        var items = await query
            .OrderBy(x => x.Type)
            .ThenBy(x => x.Code)
            .Select(x => new UnitOfMeasureDto(x.Code, x.Name, x.Type))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    public sealed record UnitOfMeasureDto(string Code, string Name, string Type);
}
