using LKvitai.MES.BuildingBlocks.SharedKernel;
using LKvitai.MES.Modules.Warehouse.Api.Security;
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
    private readonly IMediator _mediator;

    public AgnumImportController(
        WarehouseDbContext dbContext,
        IAgnumNomenclatureImportService nomenclatureImportService,
        IAgnumBalanceImportService balanceImportService,
        IAgnumDistributionService distributionService,
        IMediator mediator)
    {
        _dbContext = dbContext;
        _nomenclatureImportService = nomenclatureImportService;
        _balanceImportService = balanceImportService;
        _distributionService = distributionService;
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

        var balances = balanceRows
            .Select(x =>
            {
                var distributedQty = distributedLookup.GetValueOrDefault(x.Id);
                return new AgnumVirtualBalanceRow(
                    x.Id,
                    x.AgnumProductId,
                    x.Sku,
                    x.Quantity,
                    distributedQty,
                    x.Quantity - distributedQty,
                    x.Uom,
                    x.ItemId,
                    x.ImportedAt);
            })
            .ToList();

        return Ok(new { SndId = sndId, RunId = latestRun.Id, ImportedAt = latestRun.FinishedAt, Balances = balances });
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

    private sealed record AgnumVirtualBalanceRow(
        Guid Id,
        int AgnumProductId,
        string? Sku,
        decimal Quantity,
        decimal DistributedQty,
        decimal RemainingQty,
        string Uom,
        int? ItemId,
        DateTime ImportedAt);

    public sealed record DistributeAgnumBalanceRequest(
        string LocationCode,
        string WarehouseId,
        decimal Quantity,
        Guid OperatorId);
}
