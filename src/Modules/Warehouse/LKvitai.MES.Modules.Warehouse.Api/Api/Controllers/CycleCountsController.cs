using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/cycle-counts")]
public sealed class CycleCountsController : ControllerBase
{
    private const string DefaultWarehouseId = "WH1";
    private readonly IMediator _mediator;
    private readonly WarehouseDbContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CycleCountsController(
        IMediator mediator,
        WarehouseDbContext dbContext,
        IEventBus eventBus,
        ICurrentUserService currentUserService,
        IHttpContextAccessor httpContextAccessor)
    {
        _mediator = mediator;
        _dbContext = dbContext;
        _eventBus = eventBus;
        _currentUserService = currentUserService;
        _httpContextAccessor = httpContextAccessor;
    }

    [HttpGet]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetListAsync(CancellationToken cancellationToken = default)
    {
        var cycleCounts = await _dbContext.CycleCounts
            .AsNoTracking()
            .Include(x => x.Lines)
            .OrderByDescending(x => x.ScheduledDate)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(cycleCounts.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cycleCount = await _dbContext.CycleCounts
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (cycleCount is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, "Cycle count not found."));
        }

        return Ok(ToResponse(cycleCount));
    }

    [HttpPost("schedule")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> ScheduleAsync(
        [FromBody] ScheduleCycleCountRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        var commandId = request.CommandId == Guid.Empty ? Guid.NewGuid() : request.CommandId;
        var result = await _mediator.Send(new ScheduleCycleCountCommand
        {
            CommandId = commandId,
            CorrelationId = ResolveCorrelationId(),
            ScheduledDate = request.ScheduledDate,
            AbcClass = request.AbcClass,
            LocationIds = request.LocationIds ?? Array.Empty<int>(),
            AssignedOperator = request.AssignedOperator
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        var cycleCount = await _dbContext.CycleCounts
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.ScheduleCommandId == commandId, cancellationToken);
        if (cycleCount is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.InternalError, "Cycle count not found after scheduling."));
        }

        return Ok(ToResponse(cycleCount));
    }

    [HttpPost("{id:guid}/record-count")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> RecordCountAsync(
        Guid id,
        [FromBody] RecordCountRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        var location = await ResolveLocationAsync(request, cancellationToken);
        if (location is null)
        {
            return ValidationFailure("LocationCode or valid LocationId is required.");
        }

        var item = await ResolveItemAsync(request, cancellationToken);
        if (item is null)
        {
            return ValidationFailure("ItemBarcode or valid ItemId is required.");
        }

        var result = await _mediator.Send(new RecordCountCommand
        {
            CommandId = request.CommandId == Guid.Empty ? Guid.NewGuid() : request.CommandId,
            CorrelationId = ResolveCorrelationId(),
            CycleCountId = id,
            LocationId = location.Id,
            ItemId = item.Id,
            PhysicalQty = request.PhysicalQty,
            Reason = request.Reason,
            CountedBy = request.CountedBy ?? string.Empty
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        var cycleCount = await _dbContext.CycleCounts
            .AsNoTracking()
            .Include(x => x.Lines)
            .ThenInclude(x => x.Location)
            .Include(x => x.Lines)
            .ThenInclude(x => x.Item)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (cycleCount is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, "Cycle count not found."));
        }

        var line = cycleCount.Lines.First(x => x.LocationId == location.Id && x.ItemId == item.Id);
        var signedPercent = line.SystemQty == 0m
            ? 0m
            : decimal.Round((line.Delta / line.SystemQty) * 100m, 2, MidpointRounding.AwayFromZero);
        var hasDiscrepancy = Math.Abs(line.Delta) > 10m || Math.Abs(signedPercent) > 5m;
        var warning = hasDiscrepancy
            ? $"Discrepancy detected: {line.Delta:0.###} units ({signedPercent:0.##}%)"
            : null;

        return Ok(new RecordCountResponse(ToResponse(cycleCount), hasDiscrepancy, warning));
    }

    [HttpGet("{id:guid}/lines")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetLinesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cycleCount = await _dbContext.CycleCounts
            .AsNoTracking()
            .Include(x => x.Lines)
            .ThenInclude(x => x.Location)
            .Include(x => x.Lines)
            .ThenInclude(x => x.Item)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (cycleCount is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, "Cycle count not found."));
        }

        return Ok(cycleCount.Lines
            .OrderBy(x => x.LocationId)
            .ThenBy(x => x.ItemId)
            .Select(x => new CycleCountLineDetailResponse(
                x.Id,
                x.LocationId,
                x.Location?.Code ?? x.LocationId.ToString(),
                x.ItemId,
                x.Item?.InternalSKU ?? x.ItemId.ToString(),
                x.SystemQty,
                x.PhysicalQty,
                x.Delta,
                x.CountedAt,
                x.CountedBy,
                x.Status.ToString().ToUpperInvariant(),
                x.Reason))
            .ToList());
    }

    [HttpGet("{id:guid}/discrepancies")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetDiscrepanciesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cycleCount = await _dbContext.CycleCounts
            .AsNoTracking()
            .Include(x => x.Lines)
            .ThenInclude(x => x.Location)
            .Include(x => x.Lines)
            .ThenInclude(x => x.Item)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (cycleCount is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, "Cycle count not found."));
        }

        var unitCostLookup = await _dbContext.OnHandValues
            .AsNoTracking()
            .GroupBy(x => x.ItemId)
            .Select(x => new { ItemId = x.Key, UnitCost = x.OrderBy(y => y.LastUpdated).Select(y => y.UnitCost).LastOrDefault() })
            .ToDictionaryAsync(x => x.ItemId, x => x.UnitCost, cancellationToken);

        var rows = cycleCount.Lines
            .Select(line =>
            {
                unitCostLookup.TryGetValue(line.ItemId, out var unitCost);
                var variancePercent = line.SystemQty == 0m
                    ? (line.Delta == 0m ? 0m : 100m)
                    : decimal.Round((line.Delta / line.SystemQty) * 100m, 2, MidpointRounding.AwayFromZero);
                var valueImpact = decimal.Round(line.Delta * unitCost, 2, MidpointRounding.AwayFromZero);
                return new DiscrepancyLineResponse(
                    line.Id,
                    line.LocationId,
                    line.Location?.Code ?? line.LocationId.ToString(),
                    line.ItemId,
                    line.Item?.InternalSKU ?? line.ItemId.ToString(),
                    line.SystemQty,
                    line.PhysicalQty,
                    line.Delta,
                    variancePercent,
                    valueImpact,
                    line.AdjustmentApprovedBy,
                    line.AdjustmentApprovedAt);
            })
            .Where(x => Math.Abs(x.VariancePercent) > 5m || Math.Abs(x.Variance) > 10m)
            .OrderBy(x => x.LocationCode)
            .ThenBy(x => x.ItemCode)
            .ToList();

        return Ok(rows);
    }

    [HttpPost("{id:guid}/approve-adjustment")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> ApproveAdjustmentAsync(
        Guid id,
        [FromBody] ApproveAdjustmentRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || request.LineIds is null || request.LineIds.Count == 0)
        {
            return ValidationFailure("LineIds are required.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ValidationFailure("Reason is required.");
        }

        var cycleCount = await _dbContext.CycleCounts
            .Include(x => x.Lines)
            .ThenInclude(x => x.Location)
            .Include(x => x.Lines)
            .ThenInclude(x => x.Item)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (cycleCount is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, "Cycle count not found."));
        }

        var targetLineIds = request.LineIds.Distinct().ToHashSet();
        var lines = cycleCount.Lines
            .Where(x => targetLineIds.Contains(x.Id))
            .ToList();
        if (lines.Count == 0)
        {
            return ValidationFailure("No matching discrepancy lines found.");
        }

        var unitCostLookup = await _dbContext.OnHandValues
            .AsNoTracking()
            .GroupBy(x => x.ItemId)
            .Select(x => new { ItemId = x.Key, UnitCost = x.OrderBy(y => y.LastUpdated).Select(y => y.UnitCost).LastOrDefault() })
            .ToDictionaryAsync(x => x.ItemId, x => x.UnitCost, cancellationToken);

        var approvedBy = string.IsNullOrWhiteSpace(request.ApprovedBy)
            ? _currentUserService.GetCurrentUserId()
            : request.ApprovedBy.Trim();
        var isCfo = _httpContextAccessor.HttpContext?.User.IsInRole(WarehouseRoles.CFO) == true;
        foreach (var line in lines)
        {
            unitCostLookup.TryGetValue(line.ItemId, out var unitCost);
            var valueImpact = Math.Abs(decimal.Round(line.Delta * unitCost, 2, MidpointRounding.AwayFromZero));
            if (valueImpact > 1000m && !isCfo)
            {
                return ValidationFailure("CFO approval required for adjustments > $1000.");
            }
        }

        var operatorId = _currentUserService.GetCurrentUserId();
        foreach (var line in lines)
        {
            await _eventBus.PublishAsync(new StockAdjustedEvent
            {
                AggregateId = cycleCount.Id,
                UserId = operatorId,
                WarehouseId = DefaultWarehouseId,
                AdjustmentId = Guid.NewGuid(),
                ItemId = line.ItemId,
                SKU = line.Item?.InternalSKU ?? string.Empty,
                LocationId = line.LocationId,
                Location = line.Location?.Code ?? line.LocationId.ToString(),
                QtyDelta = line.Delta,
                ReasonCode = "CYCLE_COUNT",
                Notes = request.Reason,
                Timestamp = DateTime.UtcNow
            }, cancellationToken);

            line.AdjustmentApprovedBy = approvedBy;
            line.AdjustmentApprovedAt = DateTimeOffset.UtcNow;
            line.Status = CycleCountLineStatus.Approved;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ApproveAdjustmentResponse(lines.Count, approvedBy, DateTimeOffset.UtcNow));
    }

    [HttpPost("{id:guid}/apply-adjustment")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> ApplyAdjustmentAsync(
        Guid id,
        [FromBody] ApplyAdjustmentRequest? request,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ApplyAdjustmentCommand
        {
            CommandId = request?.CommandId == Guid.Empty ? Guid.NewGuid() : request?.CommandId ?? Guid.NewGuid(),
            CorrelationId = ResolveCorrelationId(),
            CycleCountId = id,
            ApproverId = request?.ApproverId
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        var cycleCount = await _dbContext.CycleCounts
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (cycleCount is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, "Cycle count not found."));
        }

        return Ok(ToResponse(cycleCount));
    }

    private static CycleCountResponse ToResponse(CycleCount cycleCount)
    {
        return new CycleCountResponse(
            cycleCount.Id,
            cycleCount.CountNumber,
            cycleCount.Status.ToString().ToUpperInvariant(),
            cycleCount.ScheduledDate,
            cycleCount.AbcClass,
            cycleCount.AssignedOperator,
            cycleCount.StartedAt,
            cycleCount.CompletedAt,
            cycleCount.CreatedBy,
            cycleCount.CreatedAt,
            cycleCount.CountedBy,
            cycleCount.ApprovedBy,
            cycleCount.Lines.Select(x => x.LocationId).Distinct().OrderBy(x => x).ToList(),
            cycleCount.Lines.Select(x => new CycleCountLineResponse(
                x.LocationId,
                x.ItemId,
                x.SystemQty,
                x.PhysicalQty,
                x.Delta,
                x.CountedAt,
                x.CountedBy,
                x.Status.ToString().ToUpperInvariant(),
                x.Reason,
                x.AdjustmentApprovedBy,
                x.AdjustmentApprovedAt)).ToList());
    }

    private async Task<Location?> ResolveLocationAsync(RecordCountRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.LocationCode))
        {
            var locationCode = request.LocationCode.Trim();
            return await _dbContext.Locations
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == locationCode, cancellationToken);
        }

        if (request.LocationId is not null)
        {
            return await _dbContext.Locations
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.LocationId.Value, cancellationToken);
        }

        return null;
    }

    private async Task<Item?> ResolveItemAsync(RecordCountRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.ItemBarcode))
        {
            var barcode = request.ItemBarcode.Trim();
            return await _dbContext.Items
                .AsNoTracking()
                .Include(x => x.Barcodes)
                .FirstOrDefaultAsync(
                    x => x.InternalSKU == barcode ||
                         x.PrimaryBarcode == barcode ||
                         x.Barcodes.Any(b => b.Barcode == barcode),
                    cancellationToken);
        }

        if (request.ItemId is not null)
        {
            return await _dbContext.Items
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.ItemId.Value, cancellationToken);
        }

        return null;
    }

    private ObjectResult Failure(Result result)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(result, HttpContext);
        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };
    }

    private ObjectResult ValidationFailure(string detail)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(
            DomainErrorCodes.ValidationError,
            detail,
            HttpContext);

        return new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status400BadRequest
        };
    }

    private Guid ResolveCorrelationId()
    {
        var raw = HttpContext.Items[CorrelationIdMiddleware.HeaderName]?.ToString();
        return Guid.TryParse(raw, out var parsed) ? parsed : Guid.NewGuid();
    }

    public sealed record ScheduleCycleCountRequest(
        Guid CommandId,
        DateTimeOffset ScheduledDate,
        string AbcClass,
        string AssignedOperator,
        IReadOnlyList<int>? LocationIds = null);

    public sealed record RecordCountRequest(
        Guid CommandId,
        decimal PhysicalQty,
        string? LocationCode = null,
        string? ItemBarcode = null,
        int? LocationId = null,
        int? ItemId = null,
        string? Reason = null,
        string? CountedBy = null);

    public sealed record RecordCountResponse(
        CycleCountResponse CycleCount,
        bool HasDiscrepancy,
        string? Warning);

    public sealed record ApproveAdjustmentRequest(
        Guid CommandId,
        IReadOnlyList<Guid> LineIds,
        string? ApprovedBy,
        string Reason);

    public sealed record ApproveAdjustmentResponse(
        int ApprovedLineCount,
        string ApprovedBy,
        DateTimeOffset ApprovedAt);

    public sealed record DiscrepancyLineResponse(
        Guid LineId,
        int LocationId,
        string LocationCode,
        int ItemId,
        string ItemCode,
        decimal SystemQty,
        decimal PhysicalQty,
        decimal Variance,
        decimal VariancePercent,
        decimal ValueImpact,
        string? AdjustmentApprovedBy,
        DateTimeOffset? AdjustmentApprovedAt);

    public sealed record ApplyAdjustmentRequest(Guid CommandId, string? ApproverId);

    public sealed record CycleCountLineDetailResponse(
        Guid Id,
        int LocationId,
        string LocationCode,
        int ItemId,
        string ItemBarcode,
        decimal SystemQty,
        decimal PhysicalQty,
        decimal Delta,
        DateTimeOffset? CountedAt,
        string? CountedBy,
        string Status,
        string? Reason,
        string? AdjustmentApprovedBy = null,
        DateTimeOffset? AdjustmentApprovedAt = null);

    public sealed record CycleCountLineResponse(
        int LocationId,
        int ItemId,
        decimal SystemQty,
        decimal PhysicalQty,
        decimal Delta,
        DateTimeOffset? CountedAt,
        string? CountedBy,
        string Status,
        string? Reason,
        string? AdjustmentApprovedBy = null,
        DateTimeOffset? AdjustmentApprovedAt = null);

    public sealed record CycleCountResponse(
        Guid Id,
        string CountNumber,
        string Status,
        DateTimeOffset ScheduledDate,
        string AbcClass,
        string AssignedOperator,
        DateTimeOffset? StartedAt,
        DateTimeOffset? CompletedAt,
        string? CreatedBy,
        DateTimeOffset CreatedAt,
        string? CountedBy,
        string? ApprovedBy,
        IReadOnlyList<int> LocationIds,
        IReadOnlyList<CycleCountLineResponse> Lines);
}
