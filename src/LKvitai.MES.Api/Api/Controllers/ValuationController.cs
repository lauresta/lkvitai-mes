using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Application.Commands;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Domain.Aggregates;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using Marten;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/valuation")]
public sealed class ValuationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly WarehouseDbContext _dbContext;
    private readonly IDocumentStore _documentStore;
    private readonly IAvailableStockQuantityResolver _quantityResolver;

    public ValuationController(
        IMediator mediator,
        WarehouseDbContext dbContext,
        IDocumentStore documentStore,
        IAvailableStockQuantityResolver quantityResolver)
    {
        _mediator = mediator;
        _dbContext = dbContext;
        _documentStore = documentStore;
        _quantityResolver = quantityResolver;
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
        LKvitai.MES.Domain.Entities.Item item,
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

    public sealed record AdjustCostRequest(
        Guid CommandId,
        decimal NewUnitCost,
        string Reason,
        Guid? ApproverId);

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
}
