using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/handling-unit-types")]
public sealed class HandlingUnitTypesController : ControllerBase
{
    private readonly WarehouseDbContext _dbContext;

    public HandlingUnitTypesController(WarehouseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetAsync([FromQuery] string? search, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.HandlingUnitTypes
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Code.ToLower().Contains(normalized) ||
                x.Name.ToLower().Contains(normalized));
        }

        var items = await query
            .OrderBy(x => x.Code)
            .Select(x => new HandlingUnitTypeDto(x.Code, x.Name))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    public sealed record HandlingUnitTypeDto(string Code, string Name);
}
