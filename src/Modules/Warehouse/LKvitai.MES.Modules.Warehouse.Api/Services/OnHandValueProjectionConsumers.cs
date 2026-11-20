using System.Diagnostics;
using System.Diagnostics.Metrics;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Caching;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using MassTransit;
using Marten;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Pricing;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

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

    private readonly WarehouseDbContext _dbContext;

    public MartenAvailableStockQuantityResolver(IDocumentStore documentStore, WarehouseDbContext dbContext)
    {
        _documentStore = documentStore;
        _dbContext = dbContext;
    }

    public async Task<decimal> ResolveTotalQtyAsync(int itemId, string itemSku, CancellationToken cancellationToken)
    {
        await using var session = _documentStore.QuerySession();

        // A receipt and its valuation are written before the async stock projection catches up.
        // Read committed operational events instead of freezing the previous projected quantity.
        var normalizedSku = itemSku.ToLowerInvariant();
        var events = new List<object>();
        events.AddRange(await CommittedEventData.ReadAsync<GoodsReceivedEvent>(session, normalizedSku, cancellationToken));
        events.AddRange(await CommittedEventData.ReadAsync<StockAdjustedEvent>(session, normalizedSku, cancellationToken));
        events.AddRange(await CommittedEventData.ReadAsync<StockMovedEvent>(session, normalizedSku, cancellationToken));
        events.AddRange(await CommittedEventData.ReadAsync<PickCompletedEvent>(session, normalizedSku, cancellationToken));
        events.AddRange(await CommittedEventData.ReadAsync<QCPassedEvent>(session, normalizedSku, cancellationToken));
        events.AddRange(await CommittedEventData.ReadAsync<QCFailedEvent>(session, normalizedSku, cancellationToken));
        var locations = await EntityFrameworkQueryableExtensions.ToListAsync(
            _dbContext.Locations.AsNoTracking(), cancellationToken);
        return StockEventQuantity.Calculate(events, itemSku, locations);
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
                g => g.Sum(x => x.OnHandQty),
                StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Builds the OnHandValue read model from <see cref="StockValueChanged"/> only: Qty = valued quantity,
/// TotalValue = value in EUR, UnitCost = value / quantity (last known unit cost when nothing is valued).
/// </summary>
public sealed class OnHandValueProjectionConsumer : IConsumer<StockValueChanged>
{
    private readonly WarehouseDbContext _dbContext;
    private readonly ICacheService _cacheService;
    private readonly ILogger<OnHandValueProjectionConsumer> _logger;

    public OnHandValueProjectionConsumer(
        WarehouseDbContext dbContext,
        ICacheService cacheService,
        ILogger<OnHandValueProjectionConsumer> logger)
    {
        _dbContext = dbContext;
        _cacheService = cacheService;
        _logger = logger;
    }

    public OnHandValueProjectionConsumer(
        WarehouseDbContext dbContext,
        ILogger<OnHandValueProjectionConsumer> logger)
        : this(dbContext, new NoOpCacheService(), logger)
    {
    }

    public async Task Consume(ConsumeContext<StockValueChanged> context)
    {
        await StockValueProjection.ApplyAsync(_dbContext, _logger, [context.Message], context.CancellationToken);
        await _cacheService.RemoveAsync($"value:{context.Message.ItemId}", context.CancellationToken);
    }
}

internal static class StockValueProjection
{
    private const string ProjectionName = "OnHandValue";

    /// <summary>Applies value events to the OnHandValue rows once per event id (idempotent).</summary>
    public static async Task ApplyAsync(
        WarehouseDbContext db,
        ILogger logger,
        IEnumerable<StockValueChanged> events,
        CancellationToken cancellationToken)
    {
        foreach (var evt in events)
        {
            if (!await ProjectionConsumerMetrics.TryRegisterEventAsync(
                    db, nameof(OnHandValueProjectionConsumer), evt.EventId, cancellationToken))
            {
                continue;
            }

            var row = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                db.OnHandValues, x => x.ItemId == evt.ItemId, cancellationToken);
            if (row is null)
            {
                var item = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                    db.Items.AsNoTracking(), x => x.Id == evt.ItemId, cancellationToken);
                if (item is null)
                {
                    await OnHandValueProjectionMetrics.SaveProjectionAsync(
                        db, logger, ProjectionName, nameof(StockValueChanged), Guid.Empty,
                        evt.Timestamp, evt.CorrelationId, cancellationToken);
                    continue;
                }

                row = new OnHandValue
                {
                    Id = ValuationItemId.ToValuationItemId(item.Id),
                    ItemId = item.Id,
                    ItemSku = item.InternalSKU,
                    ItemName = item.Name,
                    CategoryId = item.CategoryId,
                    CategoryName = item.CategoryId == null
                        ? null
                        : await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                            db.ItemCategories.AsNoTracking().Where(c => c.Id == item.CategoryId).Select(c => c.Name),
                            cancellationToken)
                };
                db.OnHandValues.Add(row);
            }

            var priorUnitCost = row.UnitCost;
            row.Qty += evt.QtyDelta;
            row.TotalValue += evt.ValueDeltaEur;
            row.UnitCost = row.Qty > 0m
                ? decimal.Round(row.TotalValue / row.Qty, 4, MidpointRounding.AwayFromZero)
                : priorUnitCost;
            row.LastUpdated = DateTime.SpecifyKind(evt.Timestamp, DateTimeKind.Utc);

            await OnHandValueProjectionMetrics.SaveProjectionAsync(
                db, logger, ProjectionName, nameof(StockValueChanged), row.Id,
                evt.Timestamp, evt.CorrelationId, cancellationToken);
        }
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
    private static readonly Meter ProjectionMeter = new("LKvitai.MES.Modules.Warehouse.Projections.OnHandValue");
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
