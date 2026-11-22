using LKvitai.MES.BuildingBlocks.SharedKernel;
using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Domain.Purchasing;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Caching;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

/// <summary>Sets Intrastat item data (commodity code, origin country, supplementary unit) on many items at once.</summary>
[ApiController]
[Route("api/warehouse/v1/items/intrastat-bulk")]
public sealed class ItemIntrastatBulkController : ControllerBase
{
    /// <summary>Upper bound of items one request may touch, so a single call cannot lock the whole catalogue.</summary>
    public const int MaxItemsPerRequest = 5000;

    private readonly WarehouseDbContext _dbContext;

    public ItemIntrastatBulkController(WarehouseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>Items are chosen either by id or by the same filter the items list uses.</summary>
    public sealed record ItemSelectionFilter(string? Search, int? CategoryId, string? Status, bool IntrastatMissing);

    /// <summary>Only the provided (non-blank) values are applied. With <c>DryRun</c> nothing is saved, the counts are returned.</summary>
    public sealed record BulkIntrastatRequestDto(
        IReadOnlyList<int>? ItemIds,
        ItemSelectionFilter? Filter,
        string? CommodityCode,
        string? OriginCountry,
        string? SupplementaryUnit,
        decimal? SupplementaryUnitFactor,
        bool DryRun = false);

    public sealed record BulkIntrastatResultDto(int Matched, int Changed, int Unchanged, bool DryRun);

    [HttpPost]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> SetAsync(
        [FromBody] BulkIntrastatRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var invalid = ValidateRequest(request);
        if (invalid is not null)
        {
            return ValidationFailure(invalid);
        }

        var query = BuildQuery(request);
        var matched = await query.CountAsync(cancellationToken);
        if (matched > MaxItemsPerRequest)
        {
            return ValidationFailure($"{matched} items match; at most {MaxItemsPerRequest} can be changed in one request. Narrow the selection.");
        }

        var items = await query.ToListAsync(cancellationToken);
        var changedItems = items
            .Where(i => IntrastatItemRules.Apply(i, request.CommodityCode, request.OriginCountry, request.SupplementaryUnit, request.SupplementaryUnitFactor))
            .ToList();

        if (request.DryRun)
        {
            _dbContext.ChangeTracker.Clear();
        }
        else if (changedItems.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            foreach (var item in changedItems)
            {
                await Cache.RemoveAsync($"item:{item.Id}", cancellationToken);
            }
        }

        return Ok(new BulkIntrastatResultDto(items.Count, changedItems.Count, items.Count - changedItems.Count, request.DryRun));
    }

    private static string? ValidateRequest(BulkIntrastatRequestDto request)
    {
        if (request.ItemIds is not { Count: > 0 } && request.Filter is null)
        {
            return "Select items by id or by filter.";
        }

        if (IntrastatItemRules.Normalize(request.CommodityCode) is null
            && IntrastatItemRules.Normalize(request.OriginCountry) is null
            && IntrastatItemRules.Normalize(request.SupplementaryUnit) is null
            && request.SupplementaryUnitFactor is null)
        {
            return "Fill at least one Intrastat field to set.";
        }

        var problems = IntrastatItemRules.Validate(
            request.CommodityCode, request.OriginCountry, request.SupplementaryUnit, request.SupplementaryUnitFactor);
        return problems.Count == 0 ? null : string.Join(" ", problems.Select(p => p.Message));
    }

    private IQueryable<Item> BuildQuery(BulkIntrastatRequestDto request)
    {
        var query = _dbContext.Items.AsQueryable();
        if (request.ItemIds is { Count: > 0 })
        {
            var ids = request.ItemIds.Distinct().ToList();
            return query.Where(i => ids.Contains(i.Id));
        }

        var filter = request.Filter!;
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            query = query.WhereMatches(_dbContext, filter.Search.Trim().ToLowerInvariant());
        }

        if (filter.CategoryId.HasValue)
        {
            query = query.Where(i => i.CategoryId == filter.CategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var status = filter.Status.Trim();
            query = query.Where(i => i.Status == status);
        }

        return filter.IntrastatMissing ? query.Where(IntrastatItemRules.MissingData) : query;
    }

    private ICacheService Cache => HttpContext?.RequestServices?.GetService<ICacheService>() ?? new LKvitai.MES.Modules.Warehouse.Infrastructure.Caching.NoOpCacheService();

    private ObjectResult ValidationFailure(string detail)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(DomainErrorCodes.ValidationError, detail, HttpContext);
        return new ObjectResult(problemDetails) { StatusCode = StatusCodes.Status400BadRequest };
    }
}
