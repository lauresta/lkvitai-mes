using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Api.Security;
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

    public ValuationController(
        IMediator mediator,
        WarehouseDbContext dbContext,
        IDocumentStore documentStore)
    {
        _mediator = mediator;
        _dbContext = dbContext;
        _documentStore = documentStore;
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
}
