using System.Diagnostics;
using System.Diagnostics.Metrics;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Infrastructure.Caching;
using LKvitai.MES.Infrastructure.Persistence;
using MassTransit;
using Marten;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace LKvitai.MES.Api.Services;

public interface IAvailableStockQuantityResolver
{
    Task<decimal> ResolveTotalQtyAsync(int itemId, string itemSku, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, decimal>> ResolveQtyBySkuForLocationAsync(
        string locationCode,
        CancellationToken cancellationToken);
}

public sealed class MartenAvailableStockQuantityResolver : IAvailableStockQuantityResolver
{
    private readonly IDocumentStore _documentStore;

    public MartenAvailableStockQuantityResolver(IDocumentStore documentStore)
    {
        _documentStore = documentStore;
    }

    public async Task<decimal> ResolveTotalQtyAsync(int itemId, string itemSku, CancellationToken cancellationToken)
    {
        await using var session = _documentStore.QuerySession();

        var byItemId = await Marten.QueryableExtensions.ToListAsync(
            session.Query<AvailableStockView>().Where(x => x.ItemId == itemId),
            cancellationToken);

        if (byItemId.Count > 0)
        {
            return byItemId.Sum(x => x.AvailableQty);
        }

        var bySku = await Marten.QueryableExtensions.ToListAsync(
            session.Query<AvailableStockView>().Where(x => x.SKU == itemSku),
            cancellationToken);

        return bySku.Sum(x => x.AvailableQty);
    }

    public async Task<IReadOnlyDictionary<string, decimal>> ResolveQtyBySkuForLocationAsync(
        string locationCode,
        CancellationToken cancellationToken)
    {
        await using var session = _documentStore.QuerySession();

        var rows = await Marten.QueryableExtensions.ToListAsync(
            session.Query<AvailableStockView>().Where(x => x.Location == locationCode),
            cancellationToken);

        return rows
            .GroupBy(x => x.SKU, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x => x.AvailableQty),
                StringComparer.OrdinalIgnoreCase);
    }
}

public sealed class OnHandValueProjectionConsumer :
    IConsumer<ValuationInitialized>,
    IConsumer<CostAdjusted>,
    IConsumer<LandedCostAllocated>,
    IConsumer<StockWrittenDown>,
    IConsumer<LandedCostApplied>,
    IConsumer<WrittenDown>,
    IConsumer<StockMovedEvent>
{
    private const string ProjectionName = "OnHandValue";
    private readonly WarehouseDbContext _dbContext;
    private readonly IAvailableStockQuantityResolver _quantityResolver;
    private readonly ICacheService _cacheService;
    private readonly ILogger<OnHandValueProjectionConsumer> _logger;

    public OnHandValueProjectionConsumer(
        WarehouseDbContext dbContext,
        IAvailableStockQuantityResolver quantityResolver,
        ICacheService cacheService,
        ILogger<OnHandValueProjectionConsumer> logger)
    {
        _dbContext = dbContext;
        _quantityResolver = quantityResolver;
        _cacheService = cacheService;
        _logger = logger;
    }

    public OnHandValueProjectionConsumer(
        WarehouseDbContext dbContext,
        IAvailableStockQuantityResolver quantityResolver,
        ILogger<OnHandValueProjectionConsumer> logger)
        : this(dbContext, quantityResolver, new NoOpCacheService(), logger)
    {
    }

    public Task Consume(ConsumeContext<ValuationInitialized> context)
    {
        return UpsertFromValuationAsync(
            context.Message.EventId,
            context.Message.Timestamp,
            context.Message.CorrelationId,
            context.Message.ItemId,
            context.Message.InitialUnitCost,
            nameof(ValuationInitialized),
            context.CancellationToken);
    }

    public Task Consume(ConsumeContext<CostAdjusted> context)
    {
        return UpsertFromValuationAsync(
            context.Message.EventId,
            context.Message.Timestamp,
            context.Message.CorrelationId,
            context.Message.ItemId,
            context.Message.NewUnitCost,
            nameof(CostAdjusted),
            context.CancellationToken);
    }

    public Task Consume(ConsumeContext<LandedCostAllocated> context)
    {
        return UpsertFromValuationAsync(
            context.Message.EventId,
            context.Message.Timestamp,
            context.Message.CorrelationId,
            context.Message.ItemId,
            context.Message.NewUnitCost,
            nameof(LandedCostAllocated),
            context.CancellationToken);
    }

    public Task Consume(ConsumeContext<StockWrittenDown> context)
    {
        return UpsertFromValuationAsync(
            context.Message.EventId,
            context.Message.Timestamp,
            context.Message.CorrelationId,
            context.Message.ItemId,
            context.Message.NewUnitCost,
            nameof(StockWrittenDown),
            context.CancellationToken);
    }

    public Task Consume(ConsumeContext<LandedCostApplied> context)
    {
        return UpsertLandedCostByInventoryIdAsync(
            context.Message.EventId,
            context.Message.Timestamp,
            context.Message.CorrelationId,
            context.Message.ItemId,
            context.Message.TotalLandedCost,
            nameof(LandedCostApplied),
            context.CancellationToken);
    }

    public Task Consume(ConsumeContext<WrittenDown> context)
    {
        return UpsertFromValuationByInventoryIdAsync(
            context.Message.EventId,
            context.Message.Timestamp,
            context.Message.CorrelationId,
            context.Message.ItemId,
            context.Message.NewValue,
            nameof(WrittenDown),
            context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<StockMovedEvent> context)
    {
        var message = context.Message;
        if (!await ProjectionConsumerMetrics.TryRegisterEventAsync(
                _dbContext,
                nameof(OnHandValueProjectionConsumer),
                message.EventId,
                context.CancellationToken))
        {
            return;
        }

        var projectionQuery = _dbContext.OnHandValues.AsQueryable();
        var projection = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            projectionQuery,
            x => x.ItemSku == message.SKU,
            context.CancellationToken);

        if (projection is null)
        {
            var itemQuery = _dbContext.Items
                .AsNoTracking()
                .Include(x => x.Category);
            var item = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                itemQuery,
                x => x.InternalSKU == message.SKU,
                context.CancellationToken);

            if (item is null)
            {
                await OnHandValueProjectionMetrics.SaveProjectionAsync(
                    _dbContext,
                    _logger,
                    ProjectionName,
                    nameof(StockMovedEvent),
                    Guid.Empty,
                    message.Timestamp,
                    message.CorrelationId,
                    context.CancellationToken);
                return;
            }

            projection = new OnHandValue
            {
                Id = Valuation.ToValuationItemId(item.Id),
                ItemId = item.Id,
                ItemSku = item.InternalSKU,
                ItemName = item.Name,
                CategoryId = item.CategoryId,
                CategoryName = item.Category?.Name,
                UnitCost = 0m
            };
            _dbContext.OnHandValues.Add(projection);
        }

        var qty = await _quantityResolver.ResolveTotalQtyAsync(
            projection.ItemId,
            projection.ItemSku,
            context.CancellationToken);

        projection.Qty = qty;
        projection.TotalValue = decimal.Round(projection.Qty * projection.UnitCost, 4, MidpointRounding.AwayFromZero);
        projection.LastUpdated = DateTime.SpecifyKind(message.Timestamp, DateTimeKind.Utc);

        await OnHandValueProjectionMetrics.SaveProjectionAsync(
            _dbContext,
            _logger,
            ProjectionName,
            nameof(StockMovedEvent),
            projection.Id,
            message.Timestamp,
            message.CorrelationId,
            context.CancellationToken);

        await _cacheService.RemoveByPrefixAsync("stock:", context.CancellationToken);
        await _cacheService.RemoveAsync($"value:{projection.ItemId}", context.CancellationToken);
    }

    private async Task UpsertFromValuationAsync(
        Guid eventId,
        DateTime eventTimestampUtc,
        string? correlationId,
        Guid valuationItemId,
        decimal unitCost,
        string eventType,
        CancellationToken cancellationToken)
    {
        if (!await ProjectionConsumerMetrics.TryRegisterEventAsync(
                _dbContext,
                nameof(OnHandValueProjectionConsumer),
                eventId,
                cancellationToken))
        {
            return;
        }

        if (!Valuation.TryToInventoryItemId(valuationItemId, out var itemId))
        {
            await OnHandValueProjectionMetrics.SaveProjectionAsync(
                _dbContext,
                _logger,
                ProjectionName,
                eventType,
                valuationItemId,
                eventTimestampUtc,
                correlationId,
                cancellationToken);
            return;
        }

        var itemProjectionQuery = _dbContext.Items
            .Include(x => x.Category);
        var item = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            itemProjectionQuery,
            x => x.Id == itemId,
            cancellationToken);

        if (item is null)
        {
            await OnHandValueProjectionMetrics.SaveProjectionAsync(
                _dbContext,
                _logger,
                ProjectionName,
                eventType,
                valuationItemId,
                eventTimestampUtc,
                correlationId,
                cancellationToken);
            return;
        }

        var byItemQuery = _dbContext.OnHandValues.AsQueryable();
        var projection = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            byItemQuery,
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
                CategoryName = item.Category?.Name
            };
            _dbContext.OnHandValues.Add(projection);
        }

        var qty = await _quantityResolver.ResolveTotalQtyAsync(item.Id, item.InternalSKU, cancellationToken);
        projection.ItemSku = item.InternalSKU;
        projection.ItemName = item.Name;
        projection.CategoryId = item.CategoryId;
        projection.CategoryName = item.Category?.Name;
        projection.Qty = qty;
        projection.UnitCost = decimal.Round(unitCost, 4, MidpointRounding.AwayFromZero);
        projection.TotalValue = decimal.Round(projection.Qty * projection.UnitCost, 4, MidpointRounding.AwayFromZero);
        projection.LastUpdated = DateTime.SpecifyKind(eventTimestampUtc, DateTimeKind.Utc);

        await OnHandValueProjectionMetrics.SaveProjectionAsync(
            _dbContext,
            _logger,
            ProjectionName,
            eventType,
            projection.Id,
            eventTimestampUtc,
            correlationId,
            cancellationToken);

        await _cacheService.RemoveAsync($"value:{projection.ItemId}", cancellationToken);
    }

    private async Task UpsertFromValuationByInventoryIdAsync(
        Guid eventId,
        DateTime eventTimestampUtc,
        string? correlationId,
        int itemId,
        decimal unitCost,
        string eventType,
        CancellationToken cancellationToken)
    {
        if (itemId <= 0)
        {
            await OnHandValueProjectionMetrics.SaveProjectionAsync(
                _dbContext,
                _logger,
                ProjectionName,
                eventType,
                Guid.Empty,
                eventTimestampUtc,
                correlationId,
                cancellationToken);
            return;
        }

        var valuationItemId = Valuation.ToValuationItemId(itemId);
        await UpsertFromValuationAsync(
            eventId,
            eventTimestampUtc,
            correlationId,
            valuationItemId,
            unitCost,
            eventType,
            cancellationToken);
    }

    private async Task UpsertLandedCostByInventoryIdAsync(
        Guid eventId,
        DateTime eventTimestampUtc,
        string? correlationId,
        int itemId,
        decimal landedCostDelta,
        string eventType,
        CancellationToken cancellationToken)
    {
        if (itemId <= 0)
        {
            await OnHandValueProjectionMetrics.SaveProjectionAsync(
                _dbContext,
                _logger,
                ProjectionName,
                eventType,
                Guid.Empty,
                eventTimestampUtc,
                correlationId,
                cancellationToken);
            return;
        }

        var row = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            _dbContext.OnHandValues,
            x => x.ItemId == itemId,
            cancellationToken);

        if (row is not null)
        {
            var adjustedCost = decimal.Round(row.UnitCost + landedCostDelta, 4, MidpointRounding.AwayFromZero);
            await UpsertFromValuationByInventoryIdAsync(
                eventId,
                eventTimestampUtc,
                correlationId,
                itemId,
                adjustedCost,
                eventType,
                cancellationToken);
            return;
        }

        await UpsertFromValuationByInventoryIdAsync(
            eventId,
            eventTimestampUtc,
            correlationId,
            itemId,
            decimal.Round(landedCostDelta, 4, MidpointRounding.AwayFromZero),
            eventType,
            cancellationToken);
    }
}

internal sealed class NoOpCacheService : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => Task.FromResult<T?>(default);
    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public CacheMetricsSnapshot GetMetrics() => new(0, 0, 0, 0d, 0d, 0, 0, 0);
}

internal static class OnHandValueProjectionMetrics
{
    private static readonly Meter ProjectionMeter = new("LKvitai.MES.Projections.OnHandValue");
    private static readonly Counter<long> ProjectionUpdatesTotal =
        ProjectionMeter.CreateCounter<long>("on_hand_value_projection_updates_total");
    private static readonly Histogram<double> ProjectionUpdateDurationMs =
        ProjectionMeter.CreateHistogram<double>("on_hand_value_projection_update_duration_ms");
    private static readonly Histogram<double> ProjectionLagSeconds =
        ProjectionMeter.CreateHistogram<double>("on_hand_value_projection_lag_seconds");
    private static readonly Counter<long> ProjectionErrorsTotal =
        ProjectionMeter.CreateCounter<long>("on_hand_value_projection_errors_total");

    private static double _totalValueDollars;

    static OnHandValueProjectionMetrics()
    {
        ProjectionMeter.CreateObservableGauge(
            "on_hand_value_total_dollars",
            () => _totalValueDollars);
    }

    public static async Task SaveProjectionAsync(
        WarehouseDbContext dbContext,
        ILogger logger,
        string projectionName,
        string eventType,
        Guid entityId,
        DateTime eventTimestampUtc,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsCheckpointDuplicate(ex))
        {
            dbContext.ChangeTracker.Clear();
            logger.LogInformation(
                "Duplicate projection event skipped for {ProjectionName} and event {EventType}",
                projectionName,
                eventType);
            return;
        }
        catch (Exception ex)
        {
            ProjectionErrorsTotal.Add(
                1,
                new KeyValuePair<string, object?>("error_type", ex.GetType().Name));

            logger.LogError(
                ex,
                "OnHandValue projection update failed for event {EventType}, entity {EntityId}",
                eventType,
                entityId);
            throw;
        }

        var durationMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        var lagSeconds = Math.Max(0d, (DateTime.UtcNow - DateTime.SpecifyKind(eventTimestampUtc, DateTimeKind.Utc)).TotalSeconds);
        var totalDollars = await dbContext.OnHandValues
            .AsNoTracking()
            .SumAsync(x => (decimal?)x.TotalValue, cancellationToken) ?? 0m;
        Interlocked.Exchange(ref _totalValueDollars, (double)totalDollars);

        ProjectionUpdatesTotal.Add(
            1,
            new KeyValuePair<string, object?>("event_type", eventType));
        ProjectionUpdateDurationMs.Record(durationMs);
        ProjectionLagSeconds.Record(lagSeconds);

        logger.LogInformation(
            "OnHandValue projection updated for event {EventType}, entity {EntityId}, lag {LagSeconds:F3}s, correlation {CorrelationId}",
            eventType,
            entityId,
            lagSeconds,
            correlationId ?? string.Empty);

        if (lagSeconds > 5d)
        {
            logger.LogWarning("OnHandValue projection lag high: {LagSeconds:F3}s", lagSeconds);
        }
    }

    private static bool IsCheckpointDuplicate(DbUpdateException ex)
    {
        if (ex.InnerException is not PostgresException pgEx)
        {
            return false;
        }

        return pgEx.SqlState == PostgresErrorCodes.UniqueViolation &&
               string.Equals(pgEx.ConstraintName, "PK_event_processing_checkpoints", StringComparison.OrdinalIgnoreCase);
    }
}
