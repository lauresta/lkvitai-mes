using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/agnum")]
[Authorize(Policy = WarehousePolicies.InventoryAccountantOrManager)]
public sealed class AgnumImportController : ControllerBase
{
    private readonly WarehouseDbContext _dbContext;
    private readonly IAgnumNomenclatureImportService _nomenclatureImportService;
    private readonly IAgnumBalanceImportService _balanceImportService;
    private readonly IAgnumDistributionService _distributionService;
    private readonly Marten.IDocumentStore _documentStore;
    private readonly IMediator _mediator;

    public AgnumImportController(
        WarehouseDbContext dbContext,
        IAgnumNomenclatureImportService nomenclatureImportService,
        IAgnumBalanceImportService balanceImportService,
        IAgnumDistributionService distributionService,
        Marten.IDocumentStore documentStore,
        IMediator mediator)
    {
        _dbContext = dbContext;
        _nomenclatureImportService = nomenclatureImportService;
        _balanceImportService = balanceImportService;
        _distributionService = distributionService;
        _documentStore = documentStore;
        _mediator = mediator;
    }

    [HttpGet("virtual-warehouses")]
    public async Task<IActionResult> GetVirtualWarehousesAsync(CancellationToken ct = default)
    {
        var mappings = await _dbContext.AgnumWarehouseMappings
            .AsNoTracking()
            .OrderBy(x => x.SndId)
            .Select(x => new AgnumWarehouseResponse(
                x.SndId,
                x.AgnumName,
                x.MesVirtualWarehouseCode,
                x.ApiKeyConfigName,
                x.IsImportEnabled))
            .ToListAsync(ct);

        return Ok(mappings);
    }

    [HttpPost("import/products")]
    public async Task<IActionResult> ImportProductsAsync(
        [FromQuery] int sndId,
        [FromQuery] bool apply = false,
        CancellationToken ct = default)
    {
        if (sndId <= 0)
        {
            return BadRequest(new { Error = "sndId is required." });
        }

        if (apply)
        {
            var result = await _nomenclatureImportService.ApplyAsync(sndId, ct);
            return Ok(result);
        }

        var preview = await _nomenclatureImportService.PreviewAsync(sndId, ct);
        return Ok(preview);
    }

    [HttpPost("import/balances")]
    public async Task<IActionResult> ImportBalancesAsync(
        [FromQuery] int sndId,
        CancellationToken ct = default)
    {
        if (sndId <= 0)
        {
            return BadRequest(new { Error = "sndId is required." });
        }

        var runId = await _balanceImportService.StartImportAsync(sndId, ct);
        return Ok(new { RunId = runId });
    }

    [HttpGet("import/status/{runId:guid}")]
    public async Task<IActionResult> GetImportStatusAsync(
        Guid runId,
        CancellationToken ct = default)
    {
        try
        {
            var status = await _balanceImportService.GetRunStatusAsync(runId, ct);
            return Ok(status);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("nomenclature")]
    public async Task<IActionResult> GetNomenclatureAsync(
        [FromQuery] int sndId,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (sndId <= 0)
        {
            return BadRequest(new { Error = "sndId is required." });
        }

        var query = _dbContext.AgnumProductLinks
            .AsNoTracking()
            .Where(x => x.SndId == sndId)
            .Join(_dbContext.Items, l => l.ItemId, i => i.Id, (l, i) => new { Link = l, Item = i });

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lower = search.ToLower();
            query = query.Where(x =>
                x.Item.InternalSKU.ToLower().Contains(lower) ||
                x.Item.Name.ToLower().Contains(lower) ||
                x.Link.AgnumCode.ToLower().Contains(lower));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.Item.InternalSKU)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Link.AgnumProductId,
                x.Link.AgnumCode,
                x.Item.InternalSKU,
                x.Item.Name,
                x.Item.BaseUoM,
                x.Item.Status,
                x.Link.AgnumEnabled,
                x.Link.LastImportedAt
            })
            .ToListAsync(ct);

        return Ok(new { Total = total, Page = page, PageSize = pageSize, Items = items });
    }

    [HttpGet("balances")]
    public async Task<IActionResult> GetBalancesAsync(
        [FromQuery] int sndId,
        CancellationToken ct = default)
    {
        if (sndId <= 0)
        {
            return BadRequest(new { Error = "sndId is required." });
        }

        var latestRun = await _dbContext.AgnumBalanceImportRuns
            .AsNoTracking()
            .Where(x => x.SndId == sndId && x.Status == "Completed")
            .OrderByDescending(x => x.FinishedAt)
            .FirstOrDefaultAsync(ct);

        if (latestRun is null)
        {
            return Ok(new { SndId = sndId, RunId = (Guid?)null, Balances = Array.Empty<object>() });
        }

        var distributedByBalance = await _dbContext.AgnumBalanceDistributions
            .AsNoTracking()
            .Where(d => d.SndId == sndId)
            .GroupBy(d => d.VirtualBalanceId)
            .Select(g => new { VirtualBalanceId = g.Key, Total = g.Sum(x => x.Quantity) })
            .ToListAsync(ct);
        var distributedLookup = distributedByBalance.ToDictionary(x => x.VirtualBalanceId, x => x.Total);

        var balanceRows = await _dbContext.AgnumVirtualWarehouseBalances
            .AsNoTracking()
            .Where(x => x.ImportRunId == latestRun.Id)
            .OrderByDescending(x => x.Quantity)
            .Select(x => new
            {
                x.Id,
                x.AgnumProductId,
                x.Sku,
                x.Quantity,
                x.Uom,
                x.ItemId,
                x.ImportedAt
            })
            .ToListAsync(ct);

        var skus = balanceRows
            .Select(x => x.Sku)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var physicalStockBySku = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var physicalLocationsBySku = new Dictionary<string, List<AgnumPhysicalLocationDto>>(StringComparer.OrdinalIgnoreCase);
        if (skus.Count > 0)
        {
            await using var querySession = _documentStore.QuerySession();
            var physicalStockRows = await Marten.QueryableExtensions.ToListAsync(
                querySession.Query<AvailableStockView>().Where(x => skus.Contains(x.SKU) && x.OnHandQty > 0m),
                ct);

            physicalStockBySku = physicalStockRows
                .GroupBy(x => x.SKU, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.Sum(y => y.OnHandQty), StringComparer.OrdinalIgnoreCase);

            physicalLocationsBySku = physicalStockRows
                .GroupBy(x => x.SKU, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    x => x.Key,
                    x => x
                        .GroupBy(y => new { y.WarehouseId, LocationCode = y.LocationCode ?? y.Location })
                        .Select(g => new AgnumPhysicalLocationDto(
                            g.Key.WarehouseId,
                            g.Key.LocationCode,
                            g.Sum(y => y.OnHandQty)))
                        .Where(location => location.Qty > 0m)
                        .OrderBy(location => location.LocationCode)
                        .ToList(),
                    StringComparer.OrdinalIgnoreCase);
        }

        var balances = balanceRows
            .Select(x =>
            {
                var distributedQty = distributedLookup.GetValueOrDefault(x.Id);
                var mesPhysicalQty = string.IsNullOrWhiteSpace(x.Sku)
                    ? 0m
                    : physicalStockBySku.GetValueOrDefault(x.Sku);
                var mesLocations = string.IsNullOrWhiteSpace(x.Sku)
                    ? new List<AgnumPhysicalLocationDto>()
                    : physicalLocationsBySku.GetValueOrDefault(x.Sku, new List<AgnumPhysicalLocationDto>());

                return new AgnumVirtualWarehouseBalanceDto(
                    x.Id,
                    x.AgnumProductId,
                    x.Sku,
                    x.Quantity,
                    distributedQty,
                    x.Quantity - distributedQty,
                    mesPhysicalQty,
                    mesLocations,
                    x.Uom,
                    x.ItemId,
                    x.ImportedAt);
            })
            .ToList();

        return Ok(new { SndId = sndId, RunId = latestRun.Id, ImportedAt = latestRun.FinishedAt, Balances = balances });
    }

    [HttpGet("reconciliation")]
    public async Task<IActionResult> GetReconciliationAsync(
        [FromQuery] int sndId,
        CancellationToken ct = default)
    {
        if (sndId <= 0)
        {
            return BadRequest(new { Error = "sndId is required." });
        }

        var latestRun = await _dbContext.AgnumBalanceImportRuns
            .AsNoTracking()
            .Where(x => x.SndId == sndId && x.Status == "Completed")
            .OrderByDescending(x => x.FinishedAt)
            .FirstOrDefaultAsync(ct);

        if (latestRun is null)
        {
            return Ok(Array.Empty<AgnumReconciliationRowDto>());
        }

        var balanceRows = await _dbContext.AgnumVirtualWarehouseBalances
            .AsNoTracking()
            .Where(x => x.ImportRunId == latestRun.Id)
            .OrderBy(x => x.Sku ?? string.Empty)
            .Select(x => new
            {
                x.Id,
                x.AgnumProductId,
                x.ItemId,
                x.Sku,
                x.Quantity
            })
            .ToListAsync(ct);

        var balanceIds = balanceRows.Select(x => x.Id).ToList();
        var distributedByBalance = await _dbContext.AgnumBalanceDistributions
            .AsNoTracking()
            .Where(d => balanceIds.Contains(d.VirtualBalanceId))
            .GroupBy(d => d.VirtualBalanceId)
            .Select(g => new { VirtualBalanceId = g.Key, Total = g.Sum(x => x.Quantity) })
            .ToListAsync(ct);
        var distributedLookup = distributedByBalance.ToDictionary(x => x.VirtualBalanceId, x => x.Total);

        var itemIds = balanceRows
            .Select(x => x.ItemId)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

        var itemNameById = itemIds.Count == 0
            ? new Dictionary<int, string>()
            : await _dbContext.Items
                .AsNoTracking()
                .Where(x => itemIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var agnumProductIds = balanceRows
            .Select(x => x.AgnumProductId)
            .Distinct()
            .ToList();
        var agnumCodeByProductId = await _dbContext.AgnumProductLinks
            .AsNoTracking()
            .Where(x => x.SndId == sndId && agnumProductIds.Contains(x.AgnumProductId))
            .ToDictionaryAsync(x => x.AgnumProductId, x => x.AgnumCode, ct);

        var skus = balanceRows
            .Select(x => x.Sku)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var physicalBySku = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (skus.Count > 0)
        {
            await using var querySession = _documentStore.QuerySession();
            var physicalStockRows = await Marten.QueryableExtensions.ToListAsync(
                querySession.Query<AvailableStockView>().Where(x => skus.Contains(x.SKU) && x.OnHandQty > 0m),
                ct);

            physicalBySku = physicalStockRows
                .GroupBy(x => x.SKU, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x.Key, x => x.Sum(y => y.OnHandQty), StringComparer.OrdinalIgnoreCase);
        }

        var rows = balanceRows
            .Select(x =>
            {
                var distributedQty = distributedLookup.GetValueOrDefault(x.Id);
                var remainingQty = x.Quantity - distributedQty;
                var mesPhysicalQty = string.IsNullOrWhiteSpace(x.Sku)
                    ? 0m
                    : physicalBySku.GetValueOrDefault(x.Sku);
                var delta = mesPhysicalQty - distributedQty;
                var status = AgnumReconciliationStatusCalculator.GetStatus(x.Sku, delta);

                return new AgnumReconciliationRowDto(
                    agnumCodeByProductId.GetValueOrDefault(x.AgnumProductId, x.AgnumProductId.ToString()),
                    x.Sku,
                    x.ItemId.HasValue && itemNameById.TryGetValue(x.ItemId.Value, out var itemName) ? itemName : null,
                    x.Quantity,
                    distributedQty,
                    remainingQty,
                    mesPhysicalQty,
                    delta,
                    status);
            })
            .ToList();

        return Ok(rows);
    }

    [HttpPost("balances/{id:guid}/distribute")]
    public async Task<IActionResult> DistributeBalanceAsync(
        Guid id,
        [FromBody] DistributeAgnumBalanceRequest request,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new DistributeAgnumBalanceCommand
        {
            CorrelationId = Guid.NewGuid(),
            CausationId = Guid.NewGuid(),
            VirtualBalanceId = id,
            LocationCode = request.LocationCode,
            WarehouseId = request.WarehouseId,
            Quantity = request.Quantity,
            OperatorId = request.OperatorId
        }, ct);

        if (result.IsSuccess)
        {
            return Ok(new { });
        }

        if (result.ErrorCode == DomainErrorCodes.NotFound)
        {
            return NotFound(new { Error = result.ErrorDetail ?? result.Error });
        }

        return BadRequest(new { Error = result.ErrorDetail ?? result.Error });
    }

    [HttpGet("balances/{id:guid}/distributions")]
    public async Task<IActionResult> GetBalanceDistributionsAsync(
        Guid id,
        CancellationToken ct = default)
    {
        try
        {
            var summary = await _distributionService.GetVirtualBalanceSummaryAsync(id, ct);
            return Ok(summary.Distributions);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private sealed record AgnumWarehouseResponse(
        int SndId,
        string AgnumName,
        string MesVirtualWarehouseCode,
        string ApiKeyConfigName,
        bool IsImportEnabled);

    public sealed record AgnumVirtualWarehouseBalanceDto(
        Guid Id,
        int AgnumProductId,
        string? Sku,
        decimal Quantity,
        decimal DistributedQty,
        decimal RemainingQty,
        decimal MesPhysicalQty,
        List<AgnumPhysicalLocationDto> MesLocations,
        string Uom,
        int? ItemId,
        DateTime ImportedAt);

    public sealed record AgnumPhysicalLocationDto(string WarehouseId, string LocationCode, decimal Qty);

    public sealed record AgnumReconciliationRowDto(
        string AgnumCode,
        string? Sku,
        string? ItemName,
        decimal VirtualQty,
        decimal DistributedQty,
        decimal RemainingQty,
        decimal MesPhysicalQty,
        decimal Delta,
        string Status);

    public sealed record DistributeAgnumBalanceRequest(
        string LocationCode,
        string WarehouseId,
        decimal Quantity,
        Guid OperatorId);
}
