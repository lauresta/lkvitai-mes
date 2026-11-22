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
/// <see cref="SearchKinds.Item"/>, <see cref="SearchKinds.Supplier"/>, <see cref="SearchKinds.Location"/> and
/// <see cref="SearchKinds.Document"/> (inbound shipments, transfers). It runs the same predicates as the
/// item, supplier and location lists and carries the policy those list endpoints share.
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
        var results = new List<SearchResultDto>(perKind * 5);

        // One DbContext: the reads run one after another, each limited to perKind rows.
        results.AddRange(await ItemsAsync(normalized, perKind, cancellationToken));
        results.AddRange(await SuppliersAsync(query, normalized, perKind, cancellationToken));
        results.AddRange(await LocationsAsync(normalized, perKind, cancellationToken));
        results.AddRange(await InboundShipmentsAsync(normalized, perKind, cancellationToken));
        results.AddRange(await TransfersAsync(normalized, perKind, cancellationToken));

        return Ok(results);
    }

    private async Task<List<SearchResultDto>> ItemsAsync(string normalized, int perKind, CancellationToken cancellationToken)
    {
        var items = await _dbContext.Items
            .AsNoTracking()
            .WhereMatches(_dbContext, normalized)
            .OrderBySkuRelevance(normalized)
            .Take(perKind)
            .Select(i => new { i.Id, i.InternalSKU, i.Name, i.PrimaryBarcode })
            .ToListAsync(cancellationToken);
        if (items.Count == 0)
        {
            return [];
        }

        var itemIds = items.Select(x => x.Id).ToList();
        var mappings = await _dbContext.SupplierItemMappings
            .AsNoTracking()
            .Where(x => itemIds.Contains(x.ItemId))
            .Select(x => new { x.ItemId, x.SupplierId, x.SupplierSKU })
            .ToListAsync(cancellationToken);
        var byItem = mappings.ToLookup(x => x.ItemId, x => (x.SupplierId, x.SupplierSKU));

        return items
            .Select(item => new SearchResultDto(
                SearchKinds.Item,
                item.Name,
                JoinNonEmpty(item.InternalSKU, item.PrimaryBarcode, SupplierItemCodes.Pick(byItem[item.Id], null, normalized)),
                $"admin/items/{item.Id}",
                CodeScore(item.InternalSKU, normalized)))
            .ToList();
    }

    private async Task<List<SearchResultDto>> SuppliersAsync(string query, string normalized, int perKind, CancellationToken cancellationToken)
    {
        var suppliers = await _dbContext.Suppliers
            .AsNoTracking()
            .WhereMatches(normalized)
            .OrderBy(x => x.Code)
            .Take(perKind)
            .Select(x => new { x.Code, x.Name, x.City, x.Country })
            .ToListAsync(cancellationToken);

        return suppliers
            .Select(supplier => new SearchResultDto(
                SearchKinds.Supplier,
                supplier.Name,
                JoinNonEmpty(supplier.Code, supplier.City, supplier.Country),
                "admin/suppliers?search=" + Uri.EscapeDataString(supplier.Code),
                Math.Max(0.4, Math.Max(SearchText.Score(supplier.Name, query), SearchText.Score(supplier.Code, query)))))
            .ToList();
    }

    /// <summary>Locations by code or barcode, as the location list searches.</summary>
    private async Task<List<SearchResultDto>> LocationsAsync(string normalized, int perKind, CancellationToken cancellationToken)
    {
        var locations = await _dbContext.Locations
            .AsNoTracking()
            .Where(x => x.Code.ToLower().Contains(normalized) || x.Barcode.ToLower().Contains(normalized))
            .OrderBy(x => x.Code.ToLower() == normalized ? 0 : 1)
            .ThenBy(x => x.Code.ToLower().StartsWith(normalized) ? 0 : 1)
            .ThenBy(x => x.Code)
            .Take(perKind)
            .Select(x => new { x.Id, x.Code, x.Type, x.Status })
            .ToListAsync(cancellationToken);

        return locations
            .Select(location => new SearchResultDto(
                SearchKinds.Location,
                location.Code,
                JoinNonEmpty(location.Type, location.Status),
                $"locations/{location.Id}",
                CodeScore(location.Code, normalized)))
            .ToList();
    }

    /// <summary>Inbound shipments (purchase documents) by their number or the supplier's document number.</summary>
    private async Task<List<SearchResultDto>> InboundShipmentsAsync(string normalized, int perKind, CancellationToken cancellationToken)
    {
        var shipments = await _dbContext.InboundShipments
            .AsNoTracking()
            .Where(x => x.ReferenceNumber.ToLower().Contains(normalized)
                || (x.InvoiceNumber != null && x.InvoiceNumber.ToLower().Contains(normalized)))
            .OrderByDescending(x => x.Id)
            .Take(perKind)
            .Select(x => new
            {
                x.Id,
                x.ReferenceNumber,
                x.InvoiceNumber,
                x.Status,
                Supplier = _dbContext.Suppliers.Where(s => s.Id == x.SupplierId).Select(s => s.Name).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return shipments
            .Select(shipment => new SearchResultDto(
                SearchKinds.Document,
                shipment.ReferenceNumber,
                JoinNonEmpty(shipment.InvoiceNumber, shipment.Supplier, shipment.Status),
                $"inbound/shipments/{shipment.Id}",
                CodeScore(shipment.ReferenceNumber, normalized)))
            .ToList();
    }

    /// <summary>Transfers by transfer number.</summary>
    private async Task<List<SearchResultDto>> TransfersAsync(string normalized, int perKind, CancellationToken cancellationToken)
    {
        var transfers = await _dbContext.Transfers
            .AsNoTracking()
            .Where(x => x.TransferNumber.ToLower().Contains(normalized))
            .OrderByDescending(x => x.RequestedAt)
            .Take(perKind)
            .Select(x => new { x.Id, x.TransferNumber, x.FromWarehouse, x.ToWarehouse, x.Status })
            .ToListAsync(cancellationToken);

        return transfers
            .Select(transfer => new SearchResultDto(
                SearchKinds.Document,
                transfer.TransferNumber,
                JoinNonEmpty($"{transfer.FromWarehouse} → {transfer.ToWarehouse}", transfer.Status.ToString()),
                $"transfers/{transfer.Id}/execute",
                CodeScore(transfer.TransferNumber, normalized)))
            .ToList();
    }

    /// <summary>1 for the exact code, 0.9 for a code that starts with the text, 0.6 otherwise.</summary>
    private static double CodeScore(string code, string normalized)
    {
        var lowered = code.ToLowerInvariant();
        if (lowered == normalized) return 1;
        return lowered.StartsWith(normalized, StringComparison.Ordinal) ? 0.9 : 0.6;
    }

    private static string? JoinNonEmpty(params string?[] parts)
    {
        var text = string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
        return text.Length == 0 ? null : text;
    }
}
