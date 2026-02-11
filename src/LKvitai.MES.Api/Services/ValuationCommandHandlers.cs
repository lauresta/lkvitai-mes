using System.Diagnostics;
using System.Diagnostics.Metrics;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Application.Commands;
using LKvitai.MES.Application.Services;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Domain.Aggregates;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using Marten;
using Marten.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Api.Services;

public sealed class AdjustCostCommandHandler : IRequestHandler<AdjustCostCommand, Result>
{
    private static readonly Meter ValuationMeter = new("LKvitai.MES.Valuation");
    private static readonly Counter<long> CostAdjustmentsTotal =
        ValuationMeter.CreateCounter<long>("cost_adjustments_total");
    private static readonly Histogram<double> CostAdjustmentImpactDollars =
        ValuationMeter.CreateHistogram<double>("cost_adjustment_impact_dollars");
    private static readonly Histogram<double> CostAdjustmentDurationMs =
        ValuationMeter.CreateHistogram<double>("cost_adjustment_duration_ms");
    private static readonly Counter<long> CostAdjustmentErrorsTotal =
        ValuationMeter.CreateCounter<long>("cost_adjustment_errors_total");

    private readonly WarehouseDbContext _dbContext;
    private readonly IDocumentStore _documentStore;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AdjustCostCommandHandler> _logger;

    public AdjustCostCommandHandler(
        WarehouseDbContext dbContext,
        IDocumentStore documentStore,
        ICurrentUserService currentUserService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AdjustCostCommandHandler> logger)
    {
        _dbContext = dbContext;
        _documentStore = documentStore;
        _currentUserService = currentUserService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<Result> Handle(AdjustCostCommand request, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var validationResult = CostAdjustmentRules.ValidateRequest(request);
            if (!validationResult.IsSuccess)
            {
                RecordFailure("validation");
                return validationResult;
            }

            var itemQuery = _dbContext.Items.AsNoTracking();
            var item = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                itemQuery,
                x => x.Id == request.ItemId,
                cancellationToken);

            if (item is null)
            {
                RecordFailure("item_not_found");
                return Result.Fail(DomainErrorCodes.NotFound, $"Item {request.ItemId} not found.");
            }

            var valuationItemId = Valuation.ToValuationItemId(item.Id);
            var streamId = Valuation.StreamIdFor(valuationItemId);

            await using var session = _documentStore.LightweightSession();

            var valuation = await session.Events.AggregateStreamAsync<Valuation>(streamId, token: cancellationToken);
            var streamState = await session.Events.FetchStreamStateAsync(streamId, cancellationToken);

            if (valuation is null || streamState is null)
            {
                RecordFailure("valuation_not_found");
                return Result.Fail(DomainErrorCodes.NotFound, $"Valuation for item {item.Id} is not initialized.");
            }

            var availableQty = await ResolveAvailableQtyAsync(session, item, cancellationToken);
            var costDelta = NormalizeCurrency(request.NewUnitCost - valuation.UnitCost);
            var impact = NormalizeCurrency(costDelta * availableQty);
            var absoluteImpact = Math.Abs(impact);

            var user = _httpContextAccessor.HttpContext?.User;
            var hasManagerApproval = user?.IsInRole(WarehouseRoles.WarehouseManager) == true ||
                                     user?.IsInRole(WarehouseRoles.WarehouseAdmin) == true ||
                                     user?.IsInRole(WarehouseRoles.CFO) == true;

            var hasCfoApproval = user?.IsInRole(WarehouseRoles.CFO) == true ||
                                 user?.IsInRole(WarehouseRoles.WarehouseAdmin) == true;

            var approvalValidation = CostAdjustmentRules.ValidateApproval(
                absoluteImpact,
                request.ApproverId,
                hasManagerApproval,
                hasCfoApproval);
            if (!approvalValidation.IsSuccess)
            {
                RecordFailure(approvalValidation.ErrorCode ?? "approval_validation");
                return approvalValidation;
            }

            if (absoluteImpact > 1000m)
            {
                _logger.LogWarning(
                    "Cost adjustment requires approval: Impact {Impact} > threshold. Item {ItemId}, CorrelationId {CorrelationId}",
                    absoluteImpact,
                    item.Id,
                    request.CorrelationId);
            }

            var adjustedBy = _currentUserService.GetCurrentUserId();
            var costAdjusted = valuation.AdjustCost(
                request.NewUnitCost,
                request.Reason,
                adjustedBy,
                request.CommandId,
                request.ApproverId);

            session.Events.Append(streamId, streamState.Version, costAdjusted);
            await session.SaveChangesAsync(cancellationToken);

            CostAdjustmentsTotal.Add(
                1,
                new KeyValuePair<string, object?>("approval_required", absoluteImpact > 1000m));
            CostAdjustmentImpactDollars.Record((double)absoluteImpact);
            CostAdjustmentDurationMs.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            _logger.LogInformation(
                "Cost adjustment: Item {ItemSku}, OldCost {OldCost}, NewCost {NewCost}, Impact {Impact}, ApprovedBy {ApproverId}, CorrelationId {CorrelationId}",
                item.InternalSKU,
                costAdjusted.OldUnitCost,
                costAdjusted.NewUnitCost,
                impact,
                request.ApproverId?.ToString() ?? adjustedBy,
                request.CorrelationId);

            return Result.Ok();
        }
        catch (EventStreamUnexpectedMaxEventIdException ex)
        {
            RecordFailure("concurrency_conflict");
            _logger.LogWarning(
                ex,
                "Valuation aggregate concurrency conflict for item {ItemId}",
                request.ItemId);
            return Result.Fail(DomainErrorCodes.ConcurrencyConflict, "Valuation update conflicted with another request. Retry the command.");
        }
        catch (DomainException ex)
        {
            RecordFailure(ex.ErrorCode);
            return Result.Fail(ex.ErrorCode, ex.Message);
        }
        catch (Exception ex)
        {
            RecordFailure("internal_error");
            _logger.LogError(ex, "Cost adjustment failed for item {ItemId}", request.ItemId);
            return Result.Fail(DomainErrorCodes.InternalError, "Cost adjustment failed due to an internal error.");
        }
    }

    private static decimal NormalizeCurrency(decimal value)
    {
        return decimal.Round(value, 4, MidpointRounding.AwayFromZero);
    }

    private static async Task<decimal> ResolveAvailableQtyAsync(
        IDocumentSession session,
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

    private static void RecordFailure(string errorType)
    {
        CostAdjustmentErrorsTotal.Add(
            1,
            new KeyValuePair<string, object?>("error_type", errorType));
    }
}
