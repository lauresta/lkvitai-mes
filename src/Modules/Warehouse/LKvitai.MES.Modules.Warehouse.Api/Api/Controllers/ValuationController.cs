using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Caching;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Marten;
using Marten.Events;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/valuation")]
public sealed class ValuationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly WarehouseDbContext _dbContext;
    private readonly IDocumentStore _documentStore;
    private readonly IAvailableStockQuantityResolver _quantityResolver;
    private readonly IReasonCodeService _reasonCodeService;
    private readonly IAdminUserStore _userStore;

    public ValuationController(
        IMediator mediator,
        WarehouseDbContext dbContext,
        IDocumentStore documentStore,
        IAvailableStockQuantityResolver quantityResolver,
        IReasonCodeService reasonCodeService,
        IAdminUserStore userStore)
    {
        _userStore = userStore;
        _mediator = mediator;
        _dbContext = dbContext;
        _documentStore = documentStore;
        _quantityResolver = quantityResolver;
        _reasonCodeService = reasonCodeService;
    }

    /// <summary>Revaluation ("Уценка") document: lowers the unit price of the valued stock of one item.</summary>
    [HttpPost("revaluation")]
    [Authorize(Policy = WarehousePolicies.InventoryAccountantOrManager)]
    public async Task<IActionResult> RevaluationAsync(
        [FromBody] RevaluationRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        var commandId = request.CommandId == Guid.Empty ? Guid.NewGuid() : request.CommandId;
        var result = await _mediator.Send(new RevaluationCommand
        {
            CommandId = commandId,
            CorrelationId = ResolveCorrelationId(),
            ItemId = request.ItemId,
            NewUnitPrice = request.NewUnitPrice,
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

        return Ok(new ValuationCommandAcceptedResponse(commandId, request.ItemId, "REVALUED"));
    }

    [HttpGet("on-hand-value")]
    [Authorize(Policy = WarehousePolicies.ReadWarehouse)]
    public async Task<IActionResult> GetOnHandValueAsync(
        [FromQuery] int? itemId,
        [FromQuery] int? categoryId,
        [FromQuery] string? categoryName,
        [FromQuery] int? locationId,
        [FromQuery] DateTimeOffset? dateFrom,
        [FromQuery] DateTimeOffset? dateTo,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.OnHandValues.AsNoTracking().AsQueryable();

        if (itemId.HasValue)
        {
            query = query.Where(x => x.ItemId == itemId.Value);
        }

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
    [Authorize(Policy = WarehousePolicies.ReadWarehouse)]
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
            var runningQty = 0m;
            var runningValue = 0m;

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

                    case StockValueChanged valueChanged:
                    {
                        var previous = runningQty > 0m ? decimal.Round(runningValue / runningQty, 4, MidpointRounding.AwayFromZero) : runningCost;
                        runningQty += valueChanged.QtyDelta;
                        runningValue += valueChanged.ValueDeltaEur;
                        var next = runningQty > 0m ? decimal.Round(runningValue / runningQty, 4, MidpointRounding.AwayFromZero) : previous;
                        rows.Add(new CostHistoryResponse(
                            valueChanged.EventId,
                            item.Id,
                            item.ItemSku,
                            item.ItemName,
                            NormalizeTimestamp(valueChanged.Timestamp, valueChanged.Timestamp),
                            previous,
                            next,
                            valueChanged.Reason ?? string.Empty,
                            null,
                            valueChanged.Kind.ToUpperInvariant()));
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

        var userNames = BuildUserNameLookup();
        rows = rows
            .Select(x => x with { ApprovedBy = ResolveUserName(x.ApprovedBy, userNames) })
            .ToList();

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

    private Dictionary<string, string> BuildUserNameLookup()
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var user in _userStore.GetAll())
            {
                if (!string.IsNullOrWhiteSpace(user.Username))
                {
                    lookup[user.Id.ToString()] = user.Username;
                }
            }
        }
        catch (Exception)
        {
            // Display name is cosmetic; fall back to the stored actor id when the user store is unavailable.
        }

        return lookup;
    }

    public static string? ResolveUserName(string? actor, IReadOnlyDictionary<string, string> lookup)
    {
        if (string.IsNullOrWhiteSpace(actor))
        {
            return actor;
        }

        var key = actor.Trim();
        return lookup.TryGetValue(key, out var name) ? name : key;
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

    private ICacheService Cache => HttpContext?.RequestServices?.GetService<ICacheService>() ?? new LKvitai.MES.Modules.Warehouse.Infrastructure.Caching.NoOpCacheService();

    public sealed record RevaluationRequest(
        Guid CommandId,
        int ItemId,
        decimal NewUnitPrice,
        string Reason,
        string? ApprovedBy);

    public sealed record ValuationCommandAcceptedResponse(
        Guid CommandId,
        int ItemId,
        string Status);

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
