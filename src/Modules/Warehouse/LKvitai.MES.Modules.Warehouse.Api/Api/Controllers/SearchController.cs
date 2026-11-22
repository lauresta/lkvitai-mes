using LKvitai.MES.BuildingBlocks.Search;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

/// <summary>
/// Warehouse part of the portal "Search everything" box (issue #375):
/// <c>GET /api/warehouse/v1/search?q=&amp;limit=</c> → <see cref="SearchResultDto"/>[] with kinds
/// <see cref="SearchKinds.Item"/> and <see cref="SearchKinds.Supplier"/>. It runs the same predicates
/// as the item and supplier lists and carries the same policy as those list endpoints.
/// </summary>
[ApiController]
[Route("api/warehouse/v1/search")]
public sealed class SearchController : ControllerBase
{
    private readonly WarehouseDbContext _dbContext;

    public SearchController(WarehouseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetAsync(
        [FromQuery] string? q,
        [FromQuery] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        Response?.Headers.TryAdd("Cache-Control", "no-store");

        var query = SearchRequest.NormalizeQuery(q);
        if (query is null)
        {
            return Ok(Array.Empty<SearchResultDto>());
        }

        var perKind = SearchRequest.ClampLimit(limit);
        var normalized = query.ToLowerInvariant();
        var results = new List<SearchResultDto>(perKind * 2);

        var items = await _dbContext.Items
            .AsNoTracking()
            .WhereMatches(_dbContext, normalized)
            .OrderBySkuRelevance(normalized)
            .Take(perKind)
            .Select(i => new { i.Id, i.InternalSKU, i.Name, i.PrimaryBarcode })
            .ToListAsync(cancellationToken);

        if (items.Count > 0)
        {
            var itemIds = items.Select(x => x.Id).ToList();
            var mappings = await _dbContext.SupplierItemMappings
                .AsNoTracking()
                .Where(x => itemIds.Contains(x.ItemId))
                .Select(x => new { x.ItemId, x.SupplierId, x.SupplierSKU })
                .ToListAsync(cancellationToken);
            var byItem = mappings.ToLookup(x => x.ItemId, x => (x.SupplierId, x.SupplierSKU));

            foreach (var item in items)
            {
                results.Add(new SearchResultDto(
                    SearchKinds.Item,
                    item.Name,
                    JoinNonEmpty(item.InternalSKU, item.PrimaryBarcode, SupplierItemCodes.Pick(byItem[item.Id], null, normalized)),
                    $"admin/items/{item.Id}",
                    SkuScore(item.InternalSKU, normalized)));
            }
        }

        var suppliers = await _dbContext.Suppliers
            .AsNoTracking()
            .WhereMatches(normalized)
            .OrderBy(x => x.Code)
            .Take(perKind)
            .Select(x => new { x.Code, x.Name, x.City, x.Country })
            .ToListAsync(cancellationToken);

        foreach (var supplier in suppliers)
        {
            results.Add(new SearchResultDto(
                SearchKinds.Supplier,
                supplier.Name,
                JoinNonEmpty(supplier.Code, supplier.City, supplier.Country),
                "admin/suppliers?search=" + Uri.EscapeDataString(supplier.Code),
                Math.Max(0.4, Math.Max(SearchText.Score(supplier.Name, query), SearchText.Score(supplier.Code, query)))));
        }

        return Ok(results);
    }

    private static double SkuScore(string internalSku, string normalized)
    {
        var sku = internalSku.ToLowerInvariant();
        if (sku == normalized) return 1;
        return sku.StartsWith(normalized, StringComparison.Ordinal) ? 0.9 : 0.6;
    }

    private static string? JoinNonEmpty(params string?[] parts)
    {
        var text = string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
        return text.Length == 0 ? null : text;
    }
}
