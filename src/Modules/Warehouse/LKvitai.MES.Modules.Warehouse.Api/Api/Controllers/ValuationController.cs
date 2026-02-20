using System.Security.Claims;
using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Caching;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using Marten;
using Marten.Events;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/valuation")]
public sealed class ValuationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly WarehouseDbContext _dbContext;
    private readonly IDocumentStore _documentStore;
    private readonly IAvailableStockQuantityResolver _quantityResolver;
    private readonly IReasonCodeService _reasonCodeService;
    private readonly IElectronicSignatureService _signatureService;

    public ValuationController(
        IMediator mediator,
        WarehouseDbContext dbContext,
        IDocumentStore documentStore,
        IAvailableStockQuantityResolver quantityResolver,
        IReasonCodeService reasonCodeService,
        IElectronicSignatureService signatureService)
    {
        _mediator = mediator;
        _dbContext = dbContext;
        _documentStore = documentStore;
        _quantityResolver = quantityResolver;
        _reasonCodeService = reasonCodeService;
        _signatureService = signatureService;
    }

    [HttpPost("initialize")]
    [Authorize(Policy = WarehousePolicies.InventoryAccountantOrManager)]
    public async Task<IActionResult> InitializeAsync(
        [FromBody] InitializeValuationRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        var commandId = request.CommandId == Guid.Empty ? Guid.NewGuid() : request.CommandId;
        var result = await _mediator.Send(new InitializeValuationCommand
        {
            CommandId = commandId,
            CorrelationId = ResolveCorrelationId(),
            ItemId = request.ItemId,
            InitialCost = request.InitialCost,
            Reason = request.Reason
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        return Ok(new ValuationCommandAcceptedResponse(commandId, request.ItemId, "INITIALIZED"));
    }

    [HttpPost("adjust-cost")]
    [Authorize(Policy = WarehousePolicies.InventoryAccountantOrManager)]
    public async Task<IActionResult> AdjustCostV2Async(
        [FromBody] AdjustValuationCostRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        var commandId = request.CommandId == Guid.Empty ? Guid.NewGuid() : request.CommandId;
        var result = await _mediator.Send(new AdjustValuationCostCommand
        {
            CommandId = commandId,
            CorrelationId = ResolveCorrelationId(),
            ItemId = request.ItemId,
            NewCost = request.NewCost,
            Reason = request.Reason,
            ApprovedBy = request.ApprovedBy
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        await _reasonCodeService.IncrementUsageIfCodeMatchesAsync(
            request.Reason,
            ReasonCategory.REVALUATION,
            cancellationToken);

        return Ok(new ValuationCommandAcceptedResponse(commandId, request.ItemId, "COST_ADJUSTED"));
    }

    [HttpPost("apply-landed-cost")]
    [Authorize(Policy = WarehousePolicies.InventoryAccountantOrManager)]
    public async Task<IActionResult> ApplyLandedCostAsync(
        [FromBody] ApplyLandedCostRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        var commandId = request.CommandId == Guid.Empty ? Guid.NewGuid() : request.CommandId;
        var result = await _mediator.Send(new ApplyLandedCostCommand
        {
            CommandId = commandId,
            CorrelationId = ResolveCorrelationId(),
            ShipmentId = request.ShipmentId,
            FreightCost = request.FreightCost,
            DutyCost = request.DutyCost,
            InsuranceCost = request.InsuranceCost
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        return Ok(new LandedCostCommandAcceptedResponse(commandId, request.ShipmentId));
    }

    [HttpPost("write-down")]
    [Authorize(Policy = WarehousePolicies.InventoryAccountantOrManager)]
    public async Task<IActionResult> WriteDownAsync(
        [FromBody] WriteDownRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        var commandId = request.CommandId == Guid.Empty ? Guid.NewGuid() : request.CommandId;
        var result = await _mediator.Send(new WriteDownCommand
        {
            CommandId = commandId,
            CorrelationId = ResolveCorrelationId(),
            ItemId = request.ItemId,
            NewValue = request.NewValue,
            Reason = request.Reason,
            ApprovedBy = request.ApprovedBy
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        await _reasonCodeService.IncrementUsageIfCodeMatchesAsync(
            request.Reason,
            ReasonCategory.WRITEDOWN,
            cancellationToken);

        return Ok(new ValuationCommandAcceptedResponse(commandId, request.ItemId, "WRITTEN_DOWN"));
    }

    [HttpPost("{itemId:int}/adjust-cost")]
    [Authorize(Policy = WarehousePolicies.InventoryAccountantOrManager)]
    public async Task<IActionResult> AdjustCostAsync(
        int itemId,
        [FromBody] AdjustCostRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        var commandId = request.CommandId == Guid.Empty ? Guid.NewGuid() : request.CommandId;

        var result = await _mediator.Send(new AdjustCostCommand
        {
            CommandId = commandId,
            CorrelationId = ResolveCorrelationId(),
            ItemId = itemId,
            NewUnitCost = request.NewUnitCost,
            Reason = request.Reason,
            ApproverId = request.ApproverId
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        await _reasonCodeService.IncrementUsageIfCodeMatchesAsync(
            request.Reason,
            ReasonCategory.REVALUATION,
            cancellationToken);

        var itemQuery = _dbContext.Items.AsNoTracking();
        var item = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            itemQuery,
            x => x.Id == itemId,
            cancellationToken);

        if (item is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Item {itemId} not found."));
        }

        var streamId = Valuation.StreamIdFor(Valuation.ToValuationItemId(itemId));

        await using var session = _documentStore.QuerySession();

        var costAdjustedEvent = await LoadCostAdjustedEventAsync(session, streamId, commandId, cancellationToken);
        if (costAdjustedEvent is null)
        {
            return Failure(Result.Fail(
                DomainErrorCodes.InternalError,
                "Cost adjustment event was not found after command execution."));
        }

        var availableQty = await ResolveAvailableQtyAsync(session, item, cancellationToken);
        var costDelta = decimal.Round(costAdjustedEvent.NewUnitCost - costAdjustedEvent.OldUnitCost, 4, MidpointRounding.AwayFromZero);
        var impact = decimal.Round(costDelta * availableQty, 4, MidpointRounding.AwayFromZero);

        await TryCaptureSignatureAsync(request, item.InternalSKU, impact, cancellationToken);

        return Ok(new AdjustCostResponse(
            itemId,
            item.InternalSKU,
            costAdjustedEvent.OldUnitCost,
            costAdjustedEvent.NewUnitCost,
            costDelta,
            availableQty,
            impact,
            costAdjustedEvent.Reason,
            costAdjustedEvent.ApproverId?.ToString() ?? costAdjustedEvent.AdjustedBy,
            costAdjustedEvent.AdjustedAt));
    }

    [HttpGet("on-hand-value")]
    [Authorize(Policy = WarehousePolicies.InventoryAccountantOrManager)]
    public async Task<IActionResult> GetOnHandValueAsync(
        [FromQuery] int? categoryId,
        [FromQuery] string? categoryName,
        [FromQuery] int? locationId,
        [FromQuery] DateTimeOffset? dateFrom,
        [FromQuery] DateTimeOffset? dateTo,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.OnHandValues.AsNoTracking().AsQueryable();

        if (categoryId.HasValue)
        {
            query = query.Where(x => x.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            var categoryFilter = categoryName.Trim();
            query = query.Where(x => x.CategoryName != null && x.CategoryName.Contains(categoryFilter));
        }

        if (dateFrom.HasValue)
        {
            query = query.Where(x => x.LastUpdated >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(x => x.LastUpdated <= dateTo.Value);
        }

        var orderedRows = query
            .OrderByDescending(x => x.TotalValue);
        var rows = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            orderedRows,
            cancellationToken);

        foreach (var row in rows)
        {
            await Cache.SetAsync(
                $"value:{row.ItemId}",
                new OnHandValueResponse(
                    row.Id,
                    row.ItemId,
                    row.ItemSku,
                    row.ItemName,
                    row.CategoryId,
                    row.CategoryName,
                    row.Qty,
                    row.UnitCost,
                    row.TotalValue,
                    row.LastUpdated),
                TimeSpan.FromMinutes(5),
                cancellationToken);
        }

        if (!locationId.HasValue)
        {
            return Ok(rows.Select(x => new OnHandValueResponse(
                x.Id,
                x.ItemId,
                x.ItemSku,
                x.ItemName,
                x.CategoryId,
                x.CategoryName,
                x.Qty,
                x.UnitCost,
                x.TotalValue,
                x.LastUpdated)));
        }

        var locationQuery = _dbContext.Locations
            .AsNoTracking();
        var location = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            locationQuery,
            x => x.Id == locationId.Value,
            cancellationToken);

        if (location is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Location {locationId.Value} not found."));
        }

        var qtyBySku = await _quantityResolver.ResolveQtyBySkuForLocationAsync(location.Code, cancellationToken);

        var filteredRows = rows
            .Where(x => qtyBySku.TryGetValue(x.ItemSku, out var qty) && qty > 0m)
            .Select(x =>
            {
                var qty = qtyBySku[x.ItemSku];
                var totalValue = decimal.Round(qty * x.UnitCost, 4, MidpointRounding.AwayFromZero);
                return new OnHandValueResponse(
                    x.Id,
                    x.ItemId,
                    x.ItemSku,
                    x.ItemName,
                    x.CategoryId,
                    x.CategoryName,
                    qty,
                    x.UnitCost,
                    totalValue,
                    x.LastUpdated);
            });

        return Ok(filteredRows);
    }

    [HttpGet("cost-history")]
    [Authorize(Policy = WarehousePolicies.InventoryAccountantOrManager)]
    public async Task<IActionResult> GetCostHistoryAsync(
        [FromQuery] int? itemId,
        [FromQuery] DateTimeOffset? dateFrom,
        [FromQuery] DateTimeOffset? dateTo,
        [FromQuery] string? reason,
        [FromQuery] string? approvedBy,
        CancellationToken cancellationToken = default)
    {
        var itemQuery = _dbContext.Items
            .AsNoTracking()
            .AsQueryable();

        if (itemId.HasValue)
        {
            itemQuery = itemQuery.Where(x => x.Id == itemId.Value);
        }

        var items = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            itemQuery
                .OrderBy(x => x.InternalSKU)
                .Select(x => new ItemLookup(x.Id, x.InternalSKU, x.Name)),
            cancellationToken);

        if (items.Count == 0)
        {
            return Ok(Array.Empty<CostHistoryResponse>());
        }

        await using var session = _documentStore.QuerySession();
        var rows = new List<CostHistoryResponse>();

        foreach (var item in items)
        {
            var streamId = ItemValuation.StreamIdFor(item.Id);
            IReadOnlyList<IEvent> streamEvents;
            try
            {
                streamEvents = await session.Events.FetchStreamAsync(streamId, token: cancellationToken);
            }
            catch
            {
                continue;
            }

            decimal? runningCost = null;

            foreach (var streamEvent in streamEvents)
            {
                switch (streamEvent.Data)
                {
                    case ValuationInitialized initialized:
                    {
                        var changedAt = NormalizeTimestamp(initialized.InitializedAt, initialized.Timestamp);
                        rows.Add(new CostHistoryResponse(
                            initialized.EventId,
                            item.Id,
                            item.ItemSku,
                            item.ItemName,
                            changedAt,
                            runningCost,
                            initialized.InitialUnitCost,
                            initialized.Reason,
                            initialized.InitializedBy,
                            "INITIALIZED"));

                        runningCost = initialized.InitialUnitCost;
                        break;
                    }

                    case CostAdjusted adjusted:
                    {
                        var changedAt = NormalizeTimestamp(adjusted.AdjustedAt, adjusted.Timestamp);
                        rows.Add(new CostHistoryResponse(
                            adjusted.EventId,
                            item.Id,
                            item.ItemSku,
                            item.ItemName,
                            changedAt,
                            adjusted.OldUnitCost,
                            adjusted.NewUnitCost,
                            adjusted.Reason,
                            string.IsNullOrWhiteSpace(adjusted.ApprovedBy) ? adjusted.AdjustedBy : adjusted.ApprovedBy,
                            "COST_ADJUSTED"));

                        runningCost = adjusted.NewUnitCost;
                        break;
                    }

                    case LandedCostApplied landed:
                    {
                        var previous = runningCost;
                        var next = decimal.Round((previous ?? 0m) + landed.TotalLandedCost, 4, MidpointRounding.AwayFromZero);
                        rows.Add(new CostHistoryResponse(
                            landed.EventId,
                            item.Id,
                            item.ItemSku,
                            item.ItemName,
                            NormalizeTimestamp(landed.Timestamp, landed.Timestamp),
                            previous,
                            next,
                            $"Landed cost applied for shipment {landed.ShipmentId}",
                            landed.AppliedBy,
                            "LANDED_COST_APPLIED"));

                        runningCost = next;
                        break;
                    }

                    case WrittenDown writtenDown:
                    {
                        rows.Add(new CostHistoryResponse(
                            writtenDown.EventId,
                            item.Id,
                            item.ItemSku,
                            item.ItemName,
                            NormalizeTimestamp(writtenDown.Timestamp, writtenDown.Timestamp),
                            writtenDown.OldValue,
                            writtenDown.NewValue,
                            writtenDown.Reason,
                            writtenDown.ApprovedBy,
                            "WRITTEN_DOWN"));

                        runningCost = writtenDown.NewValue;
                        break;
                    }
                }
            }
        }

        var filtered = rows.AsEnumerable();
        if (dateFrom.HasValue)
        {
            filtered = filtered.Where(x => x.ChangedAt >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            filtered = filtered.Where(x => x.ChangedAt <= dateTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(reason))
        {
            var reasonFilter = reason.Trim();
            filtered = filtered.Where(x =>
                !string.IsNullOrWhiteSpace(x.Reason) &&
                x.Reason.Contains(reasonFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(approvedBy))
        {
            var approvedByFilter = approvedBy.Trim();
            filtered = filtered.Where(x =>
                !string.IsNullOrWhiteSpace(x.ApprovedBy) &&
                x.ApprovedBy.Contains(approvedByFilter, StringComparison.OrdinalIgnoreCase));
        }

        return Ok(filtered
            .OrderByDescending(x => x.ChangedAt)
            .ThenBy(x => x.ItemSku)
            .Take(2000)
            .ToList());
    }

    private static async Task<CostAdjusted?> LoadCostAdjustedEventAsync(
        IQuerySession session,
        string streamId,
        Guid commandId,
        CancellationToken cancellationToken)
    {
        var events = await session.Events.FetchStreamAsync(streamId, token: cancellationToken);

        return events
            .Select(x => x.Data)
            .OfType<CostAdjusted>()
            .LastOrDefault(x => x.CommandId == commandId);
    }

    private static async Task<decimal> ResolveAvailableQtyAsync(
        IQuerySession session,
        LKvitai.MES.Modules.Warehouse.Domain.Entities.Item item,
        CancellationToken cancellationToken)
    {
        var rowsByItemIdQuery = session.Query<AvailableStockView>()
            .Where(x => x.ItemId == item.Id);
        var rowsByItemId = await Marten.QueryableExtensions.ToListAsync(rowsByItemIdQuery, cancellationToken);

        if (rowsByItemId.Count > 0)
        {
            return rowsByItemId.Sum(x => x.AvailableQty);
        }

        var rowsBySkuQuery = session.Query<AvailableStockView>()
            .Where(x => x.SKU == item.InternalSKU);
        var rowsBySku = await Marten.QueryableExtensions.ToListAsync(rowsBySkuQuery, cancellationToken);

        return rowsBySku.Sum(x => x.AvailableQty);
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

    private static DateTimeOffset NormalizeTimestamp(DateTime primary, DateTime fallback)
    {
        var candidate = primary == default ? fallback : primary;
        if (candidate == default)
        {
            candidate = DateTime.UtcNow;
        }

        if (candidate.Kind == DateTimeKind.Unspecified)
        {
            candidate = DateTime.SpecifyKind(candidate, DateTimeKind.Utc);
        }
        else if (candidate.Kind == DateTimeKind.Local)
        {
            candidate = candidate.ToUniversalTime();
        }

        return new DateTimeOffset(candidate, TimeSpan.Zero);
    }

    private Task TryCaptureSignatureAsync(
        AdjustCostRequest request,
        string itemSku,
        decimal impact,
        CancellationToken cancellationToken)
    {
        var requiresSignature = Math.Abs(impact) >= 10000m;
        if (!requiresSignature ||
            string.IsNullOrWhiteSpace(request.SignatureText) ||
            string.IsNullOrWhiteSpace(request.SignaturePassword))
        {
            return Task.CompletedTask;
        }

        return _signatureService.CaptureAsync(new CaptureSignatureCommand(
            "COST_ADJUSTMENT",
            itemSku,
            request.SignatureText!,
            request.SignatureMeaning ?? "APPROVED",
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown",
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            request.SignaturePassword), cancellationToken);
    }

    private ICacheService Cache => HttpContext?.RequestServices?.GetService<ICacheService>() ?? new LKvitai.MES.Modules.Warehouse.Infrastructure.Caching.NoOpCacheService();

    public sealed record AdjustCostRequest(
        Guid CommandId,
        decimal NewUnitCost,
        string Reason,
        Guid? ApproverId,
        string? SignatureText = null,
        string? SignaturePassword = null,
        string? SignatureMeaning = null);

    public sealed record InitializeValuationRequest(
        Guid CommandId,
        int ItemId,
        decimal InitialCost,
        string Reason);

    public sealed record AdjustValuationCostRequest(
        Guid CommandId,
        int ItemId,
        decimal NewCost,
        string Reason,
        string? ApprovedBy);

    public sealed record ApplyLandedCostRequest(
        Guid CommandId,
        Guid ShipmentId,
        decimal FreightCost,
        decimal DutyCost,
        decimal InsuranceCost);

    public sealed record WriteDownRequest(
        Guid CommandId,
        int ItemId,
        decimal NewValue,
        string Reason,
        string? ApprovedBy);

    public sealed record ValuationCommandAcceptedResponse(
        Guid CommandId,
        int ItemId,
        string Status);

    public sealed record LandedCostCommandAcceptedResponse(
        Guid CommandId,
        Guid ShipmentId);

    public sealed record AdjustCostResponse(
        int ItemId,
        string ItemSku,
        decimal OldUnitCost,
        decimal NewUnitCost,
        decimal CostDelta,
        decimal AvailableQty,
        decimal Impact,
        string Reason,
        string ApprovedBy,
        DateTime AdjustedAt);

    public sealed record OnHandValueResponse(
        Guid Id,
        int ItemId,
        string ItemSku,
        string ItemName,
        int? CategoryId,
        string? CategoryName,
        decimal Qty,
        decimal UnitCost,
        decimal TotalValue,
        DateTimeOffset LastUpdated);

    public sealed record CostHistoryResponse(
        Guid EventId,
        int ItemId,
        string ItemSku,
        string ItemName,
        DateTimeOffset ChangedAt,
        decimal? OldCost,
        decimal? NewCost,
        string Reason,
        string? ApprovedBy,
        string ActionType);

    private sealed record ItemLookup(
        int Id,
        string ItemSku,
        string ItemName);
}
