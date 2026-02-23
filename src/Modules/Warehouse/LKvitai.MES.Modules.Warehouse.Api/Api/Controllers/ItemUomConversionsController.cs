using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/item-uom-conversions")]
public sealed class ItemUomConversionsController : ControllerBase
{
    private readonly WarehouseDbContext _dbContext;

    public ItemUomConversionsController(WarehouseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetAsync(
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var query = from conversion in _dbContext.ItemUoMConversions.AsNoTracking()
                    join item in _dbContext.Items.AsNoTracking() on conversion.ItemId equals item.Id into itemGroup
                    from item in itemGroup.DefaultIfEmpty()
                    select new ConversionProjection(
                        conversion.Id,
                        conversion.ItemId,
                        item != null ? item.InternalSKU : string.Empty,
                        item != null ? item.Name : string.Empty,
                        conversion.FromUoM,
                        conversion.ToUoM,
                        conversion.Factor,
                        conversion.RoundingRule);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.ItemSku.ToLower().Contains(normalized) ||
                x.ItemName.ToLower().Contains(normalized) ||
                x.FromUom.ToLower().Contains(normalized) ||
                x.ToUom.ToLower().Contains(normalized));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.ItemSku)
            .ThenBy(x => x.FromUom)
            .ThenBy(x => x.ToUom)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ItemUomConversionDto(
                x.Id,
                x.ItemId,
                x.ItemSku,
                x.ItemName,
                x.FromUom,
                x.ToUom,
                x.Factor,
                x.RoundingRule))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<ItemUomConversionDto>(items, totalCount, pageNumber, pageSize));
    }

    private sealed record ConversionProjection(
        int Id,
        int ItemId,
        string ItemSku,
        string ItemName,
        string FromUom,
        string ToUom,
        decimal Factor,
        string RoundingRule);

    public sealed record ItemUomConversionDto(
        int Id,
        int ItemId,
        string ItemSku,
        string ItemName,
        string FromUom,
        string ToUom,
        decimal Factor,
        string RoundingRule);

    public sealed record PagedResponse<T>(
        IReadOnlyList<T> Items,
        int TotalCount,
        int PageNumber,
        int PageSize);
}
