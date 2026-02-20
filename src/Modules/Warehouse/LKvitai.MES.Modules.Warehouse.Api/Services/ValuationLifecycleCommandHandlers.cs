using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using Marten;
using Marten.Events;
using Marten.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using EfAsync = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public sealed class InitializeValuationCommandHandler : IRequestHandler<InitializeValuationCommand, Result>
{
    private readonly WarehouseDbContext _dbContext;
    private readonly IDocumentStore _documentStore;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAvailableStockQuantityResolver _quantityResolver;
    private readonly ILogger<InitializeValuationCommandHandler> _logger;

    public InitializeValuationCommandHandler(
        WarehouseDbContext dbContext,
        IDocumentStore documentStore,
        ICurrentUserService currentUserService,
        IAvailableStockQuantityResolver quantityResolver,
        ILogger<InitializeValuationCommandHandler> logger)
    {
        _dbContext = dbContext;
        _documentStore = documentStore;
        _currentUserService = currentUserService;
        _quantityResolver = quantityResolver;
        _logger = logger;
    }

    public async Task<Result> Handle(InitializeValuationCommand request, CancellationToken cancellationToken)
    {
        var item = await ValuationHandlerUtilities.LoadItemAsync(_dbContext, request.ItemId, cancellationToken);
        if (item is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, $"Item {request.ItemId} not found.");
        }

        var streamId = ItemValuation.StreamIdFor(item.Id);
        var actor = _currentUserService.GetCurrentUserId();

        await using var session = _documentStore.LightweightSession();
        var streamEvents = await session.Events.FetchStreamAsync(streamId, token: cancellationToken);
        if (ValuationHandlerUtilities.HasCommand(streamEvents, request.CommandId))
        {
            return Result.Ok();
        }

        if (streamEvents.Count > 0)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, $"Valuation for item {item.Id} is already initialized.");
        }

        var aggregate = new ItemValuation();
        var valuationInitialized = aggregate.Initialize(
            item.Id,
            request.InitialCost,
            request.Reason,
            actor,
            request.CommandId);

        session.Events.StartStream(streamId, valuationInitialized);
        await session.SaveChangesAsync(cancellationToken);

        await ValuationProjectionWriter.UpsertOnHandValueAsync(
            _dbContext,
            _quantityResolver,
            item,
            valuationInitialized.InitialUnitCost,
            valuationInitialized.Timestamp,
            cancellationToken);

        _logger.LogInformation(
            "Valuation initialized. ItemId {ItemId}, Cost {Cost}, Reason {Reason}, Operator {Operator}",
            item.Id,
            valuationInitialized.InitialUnitCost,
            valuationInitialized.Reason,
            actor);

        return Result.Ok();
    }
}

public sealed class AdjustValuationCostCommandHandler : IRequestHandler<AdjustValuationCostCommand, Result>
{
    private readonly WarehouseDbContext _dbContext;
    private readonly IDocumentStore _documentStore;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAvailableStockQuantityResolver _quantityResolver;
    private readonly ILogger<AdjustValuationCostCommandHandler> _logger;

    public AdjustValuationCostCommandHandler(
        WarehouseDbContext dbContext,
        IDocumentStore documentStore,
        ICurrentUserService currentUserService,
        IAvailableStockQuantityResolver quantityResolver,
        ILogger<AdjustValuationCostCommandHandler> logger)
    {
        _dbContext = dbContext;
        _documentStore = documentStore;
        _currentUserService = currentUserService;
        _quantityResolver = quantityResolver;
        _logger = logger;
    }

    public async Task<Result> Handle(AdjustValuationCostCommand request, CancellationToken cancellationToken)
    {
        var requestValidation = ValuationCostAdjustmentPolicy.ValidateRequest(request);
        if (!requestValidation.IsSuccess)
        {
            return requestValidation;
        }

        var item = await ValuationHandlerUtilities.LoadItemAsync(_dbContext, request.ItemId, cancellationToken);
        if (item is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, $"Item {request.ItemId} not found.");
        }

        var streamId = ItemValuation.StreamIdFor(item.Id);
        await using var session = _documentStore.LightweightSession();

        var streamEvents = await session.Events.FetchStreamAsync(streamId, token: cancellationToken);
        if (ValuationHandlerUtilities.HasCommand(streamEvents, request.CommandId))
        {
            return Result.Ok();
        }

        var aggregate = await session.Events.AggregateStreamAsync<ItemValuation>(streamId, token: cancellationToken);
        var streamState = await session.Events.FetchStreamStateAsync(streamId, cancellationToken);
        if (aggregate is null || streamState is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, $"Valuation for item {item.Id} is not initialized.");
        }

        try
        {
            var approvalValidation = ValuationCostAdjustmentPolicy.ValidateApproval(
                aggregate.CurrentCost,
                request.NewCost,
                request.ApprovedBy);
            if (!approvalValidation.IsSuccess)
            {
                return approvalValidation;
            }

            var actor = _currentUserService.GetCurrentUserId();
            var adjusted = aggregate.AdjustCost(
                request.NewCost,
                request.Reason,
                actor,
                request.ApprovedBy,
                request.CommandId);

            session.Events.Append(streamId, streamState.Version, adjusted);
            await session.SaveChangesAsync(cancellationToken);

            await ValuationProjectionWriter.UpsertOnHandValueAsync(
                _dbContext,
                _quantityResolver,
                item,
                adjusted.NewUnitCost,
                adjusted.Timestamp,
                cancellationToken);

            _logger.LogInformation(
                "Cost adjusted. ItemId {ItemId}, OldCost {OldCost}, NewCost {NewCost}, Reason {Reason}, ApprovedBy {ApprovedBy}",
                item.Id,
                adjusted.OldUnitCost,
                adjusted.NewUnitCost,
                adjusted.Reason,
                adjusted.ApprovedBy ?? actor);

            return Result.Ok();
        }
        catch (EventStreamUnexpectedMaxEventIdException)
        {
            return Result.Fail(DomainErrorCodes.ConcurrencyConflict, "Valuation update conflicted with another request.");
        }
        catch (DomainException ex)
        {
            return Result.Fail(ex.ErrorCode, ex.Message);
        }
    }
}

public sealed class ApplyLandedCostCommandHandler : IRequestHandler<ApplyLandedCostCommand, Result>
{
    private readonly WarehouseDbContext _dbContext;
    private readonly IDocumentStore _documentStore;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ApplyLandedCostCommandHandler> _logger;

    public ApplyLandedCostCommandHandler(
        WarehouseDbContext dbContext,
        IDocumentStore documentStore,
        ICurrentUserService currentUserService,
        ILogger<ApplyLandedCostCommandHandler> logger)
    {
        _dbContext = dbContext;
        _documentStore = documentStore;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result> Handle(ApplyLandedCostCommand request, CancellationToken cancellationToken)
    {
        if (request.FreightCost < 0m || request.DutyCost < 0m || request.InsuranceCost < 0m)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Costs must be >= 0.");
        }

        var rowsQuery = _dbContext.OnHandValues
            .AsNoTracking()
            .Where(x => x.Qty > 0m)
            .OrderBy(x => x.ItemId);
        var rows = await EfAsync.ToListAsync(rowsQuery, cancellationToken);

        if (rows.Count == 0)
        {
            return Result.Fail(DomainErrorCodes.NotFound, "No on-hand valuation rows available for landed cost allocation.");
        }

        var actor = _currentUserService.GetCurrentUserId();
        IReadOnlyList<LandedCostAllocation> allocations;
        try
        {
            allocations = LandedCostAllocationService.Allocate(
                rows,
                request.FreightCost,
                request.DutyCost,
                request.InsuranceCost);
        }
        catch (DomainException ex)
        {
            return Result.Fail(ex.ErrorCode, ex.Message);
        }

        await using var session = _documentStore.LightweightSession();
        var updatedRows = new List<OnHandValue>(rows.Count);

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var streamId = ItemValuation.StreamIdFor(row.ItemId);

            var streamEvents = await session.Events.FetchStreamAsync(streamId, token: cancellationToken);
            if (ValuationHandlerUtilities.HasCommand(streamEvents, request.CommandId))
            {
                continue;
            }

            var aggregate = await session.Events.AggregateStreamAsync<ItemValuation>(streamId, token: cancellationToken);
            if (aggregate is null)
            {
                aggregate = new ItemValuation();
                var initEvent = aggregate.Initialize(
                    row.ItemId,
                    row.UnitCost,
                    "Auto initialization for landed cost allocation",
                    actor,
                    Guid.NewGuid());
                session.Events.StartStream(streamId, initEvent);
                aggregate.Apply(initEvent);
            }

            var landedEvent = aggregate.ApplyLandedCost(
                allocations[i].FreightCost,
                allocations[i].DutyCost,
                allocations[i].InsuranceCost,
                request.ShipmentId,
                actor,
                request.CommandId);

            session.Events.Append(streamId, landedEvent);

            updatedRows.Add(new OnHandValue
            {
                Id = row.Id,
                ItemId = row.ItemId,
                ItemSku = row.ItemSku,
                ItemName = row.ItemName,
                CategoryId = row.CategoryId,
                CategoryName = row.CategoryName,
                Qty = row.Qty,
                UnitCost = decimal.Round(row.UnitCost + landedEvent.TotalLandedCost, 4, MidpointRounding.AwayFromZero),
                TotalValue = decimal.Round(row.Qty * (row.UnitCost + landedEvent.TotalLandedCost), 4, MidpointRounding.AwayFromZero),
                LastUpdated = new DateTimeOffset(landedEvent.Timestamp, TimeSpan.Zero)
            });
        }

        await session.SaveChangesAsync(cancellationToken);

        foreach (var updated in updatedRows)
        {
            _dbContext.OnHandValues.Update(updated);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Landed cost applied. ShipmentId {ShipmentId}, Rows {RowCount}, Freight {Freight}, Duty {Duty}, Insurance {Insurance}",
            request.ShipmentId,
            updatedRows.Count,
            request.FreightCost,
            request.DutyCost,
            request.InsuranceCost);

        return Result.Ok();
    }
}

public sealed class WriteDownCommandHandler : IRequestHandler<WriteDownCommand, Result>
{
    private readonly WarehouseDbContext _dbContext;
    private readonly IDocumentStore _documentStore;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAvailableStockQuantityResolver _quantityResolver;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<WriteDownCommandHandler> _logger;

    public WriteDownCommandHandler(
        WarehouseDbContext dbContext,
        IDocumentStore documentStore,
        ICurrentUserService currentUserService,
        IAvailableStockQuantityResolver quantityResolver,
        IHttpContextAccessor httpContextAccessor,
        ILogger<WriteDownCommandHandler> logger)
    {
        _dbContext = dbContext;
        _documentStore = documentStore;
        _currentUserService = currentUserService;
        _quantityResolver = quantityResolver;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<Result> Handle(WriteDownCommand request, CancellationToken cancellationToken)
    {
        var item = await ValuationHandlerUtilities.LoadItemAsync(_dbContext, request.ItemId, cancellationToken);
        if (item is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, $"Item {request.ItemId} not found.");
        }

        var streamId = ItemValuation.StreamIdFor(item.Id);
        await using var session = _documentStore.LightweightSession();

        var streamEvents = await session.Events.FetchStreamAsync(streamId, token: cancellationToken);
        if (ValuationHandlerUtilities.HasCommand(streamEvents, request.CommandId))
        {
            return Result.Ok();
        }

        var aggregate = await session.Events.AggregateStreamAsync<ItemValuation>(streamId, token: cancellationToken);
        if (aggregate is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, $"Valuation for item {item.Id} is not initialized.");
        }

        var streamState = await session.Events.FetchStreamStateAsync(streamId, cancellationToken);
        if (streamState is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, $"Valuation stream for item {item.Id} was not found.");
        }

        try
        {
            var requestValidation = ValuationWriteDownPolicy.ValidateRequest(request, aggregate.CurrentCost);
            if (!requestValidation.IsSuccess)
            {
                return requestValidation;
            }

            var currentUser = _httpContextAccessor.HttpContext?.User;
            var canApproveLargeWriteDown = currentUser?.IsInRole(WarehouseRoles.WarehouseManager) == true ||
                                           currentUser?.IsInRole(WarehouseRoles.WarehouseAdmin) == true;

            var approvalValidation = ValuationWriteDownPolicy.ValidateApproval(
                aggregate.CurrentCost,
                request.NewValue,
                request.ApprovedBy,
                canApproveLargeWriteDown);
            if (!approvalValidation.IsSuccess)
            {
                return approvalValidation;
            }

            var writer = _currentUserService.GetCurrentUserId();
            var writtenDown = aggregate.WriteDown(
                request.NewValue,
                request.Reason,
                request.ApprovedBy,
                request.CommandId);

            session.Events.Append(streamId, streamState.Version, writtenDown);
            await session.SaveChangesAsync(cancellationToken);

            await ValuationProjectionWriter.UpsertOnHandValueAsync(
                _dbContext,
                _quantityResolver,
                item,
                writtenDown.NewValue,
                writtenDown.Timestamp,
                cancellationToken);

            _logger.LogInformation(
                "Valuation write-down. ItemId {ItemId}, OldValue {OldValue}, NewValue {NewValue}, Reason {Reason}, ApprovedBy {ApprovedBy}, Operator {Operator}",
                item.Id,
                writtenDown.OldValue,
                writtenDown.NewValue,
                writtenDown.Reason,
                string.IsNullOrWhiteSpace(writtenDown.ApprovedBy) ? writer : writtenDown.ApprovedBy,
                writer);

            return Result.Ok();
        }
        catch (EventStreamUnexpectedMaxEventIdException)
        {
            return Result.Fail(DomainErrorCodes.ConcurrencyConflict, "Valuation update conflicted with another request.");
        }
        catch (DomainException ex)
        {
            return Result.Fail(ex.ErrorCode, ex.Message);
        }
    }
}

internal static class ValuationProjectionWriter
{
    public static async Task UpsertOnHandValueAsync(
        WarehouseDbContext dbContext,
        IAvailableStockQuantityResolver quantityResolver,
        Item item,
        decimal unitCost,
        DateTime timestampUtc,
        CancellationToken cancellationToken)
    {
        var projection = await EfAsync.FirstOrDefaultAsync(
            dbContext.OnHandValues,
            x => x.ItemId == item.Id,
            cancellationToken);

        if (projection is null)
        {
            projection = new OnHandValue
            {
                Id = Valuation.ToValuationItemId(item.Id),
                ItemId = item.Id,
                ItemSku = item.InternalSKU,
                ItemName = item.Name,
                CategoryId = item.CategoryId,
                CategoryName = null
            };

            dbContext.OnHandValues.Add(projection);
        }

        var qty = await quantityResolver.ResolveTotalQtyAsync(item.Id, item.InternalSKU, cancellationToken);
        projection.ItemSku = item.InternalSKU;
        projection.ItemName = item.Name;
        projection.Qty = qty;
        projection.UnitCost = decimal.Round(unitCost, 4, MidpointRounding.AwayFromZero);
        projection.TotalValue = decimal.Round(qty * projection.UnitCost, 4, MidpointRounding.AwayFromZero);
        projection.LastUpdated = new DateTimeOffset(DateTime.SpecifyKind(timestampUtc, DateTimeKind.Utc), TimeSpan.Zero);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

internal static class ValuationHandlerUtilities
{
    public static bool HasCommand(IReadOnlyList<IEvent> events, Guid commandId)
    {
        return events
            .Select(x => x.Data)
            .Any(data => data switch
            {
                ValuationInitialized initialized => initialized.CommandId == commandId,
                CostAdjusted adjusted => adjusted.CommandId == commandId,
                LandedCostApplied landed => landed.CommandId == commandId,
                WrittenDown writtenDown => writtenDown.CommandId == commandId,
                _ => false
            });
    }

    public static async Task<Item?> LoadItemAsync(
        WarehouseDbContext dbContext,
        int itemId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Items
            .Include(x => x.Category);
        return await EfAsync.FirstOrDefaultAsync(query, x => x.Id == itemId, cancellationToken);
    }
}
