using System.Diagnostics;
using System.Diagnostics.Metrics;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace LKvitai.MES.Api.Services;

public sealed class OutboundOrderSummaryConsumer :
    IConsumer<OutboundOrderCreatedEvent>,
    IConsumer<ShipmentPackedEvent>,
    IConsumer<ShipmentDispatchedEvent>
{
    private const string ProjectionName = "OutboundOrderSummary";
    private readonly WarehouseDbContext _dbContext;
    private readonly ILogger<OutboundOrderSummaryConsumer> _logger;

    public OutboundOrderSummaryConsumer(
        WarehouseDbContext dbContext,
        ILogger<OutboundOrderSummaryConsumer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OutboundOrderCreatedEvent> context)
    {
        var message = context.Message;
        if (!await ProjectionConsumerMetrics.TryRegisterEventAsync(
                _dbContext,
                nameof(OutboundOrderSummaryConsumer),
                message.EventId,
                context.CancellationToken))
        {
            return;
        }

        var summary = await _dbContext.OutboundOrderSummaries
            .FirstOrDefaultAsync(x => x.Id == message.Id, context.CancellationToken);

        if (summary is null)
        {
            summary = new OutboundOrderSummary { Id = message.Id };
            _dbContext.OutboundOrderSummaries.Add(summary);
        }

        summary.OrderNumber = message.OrderNumber;
        summary.Type = message.Type;
        summary.Status = message.Status;
        summary.CustomerName = message.CustomerName;
        summary.ItemCount = message.Lines.Count;
        summary.OrderDate = message.OrderDate;
        summary.RequestedShipDate = message.RequestedShipDate;

        await ProjectionConsumerMetrics.SaveProjectionAsync(
            _dbContext,
            _logger,
            ProjectionName,
            nameof(OutboundOrderCreatedEvent),
            message.Id,
            message.Timestamp,
            message.CorrelationId,
            context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<ShipmentPackedEvent> context)
    {
        var message = context.Message;
        if (!await ProjectionConsumerMetrics.TryRegisterEventAsync(
                _dbContext,
                nameof(OutboundOrderSummaryConsumer),
                message.EventId,
                context.CancellationToken))
        {
            return;
        }

        var summary = await _dbContext.OutboundOrderSummaries
            .FirstOrDefaultAsync(x => x.Id == message.OutboundOrderId, context.CancellationToken);

        if (summary is null)
        {
            summary = new OutboundOrderSummary
            {
                Id = message.OutboundOrderId,
                OrderNumber = message.OutboundOrderNumber,
                Type = "SALES",
                ItemCount = message.Lines.Count,
                OrderDate = message.PackedAt
            };
            _dbContext.OutboundOrderSummaries.Add(summary);
        }

        summary.Status = "PACKED";
        summary.PackedAt = message.PackedAt;
        summary.ShipmentId = message.ShipmentId;
        summary.ShipmentNumber = message.ShipmentNumber;

        await ProjectionConsumerMetrics.SaveProjectionAsync(
            _dbContext,
            _logger,
            ProjectionName,
            nameof(ShipmentPackedEvent),
            message.OutboundOrderId,
            message.Timestamp,
            message.CorrelationId,
            context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<ShipmentDispatchedEvent> context)
    {
        var message = context.Message;
        if (!await ProjectionConsumerMetrics.TryRegisterEventAsync(
                _dbContext,
                nameof(OutboundOrderSummaryConsumer),
                message.EventId,
                context.CancellationToken))
        {
            return;
        }

        var summary = await _dbContext.OutboundOrderSummaries
            .FirstOrDefaultAsync(x => x.ShipmentId == message.ShipmentId, context.CancellationToken);

        if (summary is null)
        {
            await ProjectionConsumerMetrics.SaveProjectionAsync(
                _dbContext,
                _logger,
                ProjectionName,
                nameof(ShipmentDispatchedEvent),
                message.ShipmentId,
                message.Timestamp,
                message.CorrelationId,
                context.CancellationToken);
            return;
        }

        summary.Status = "SHIPPED";
        summary.ShippedAt = message.DispatchedAt;
        summary.TrackingNumber = message.TrackingNumber;

        await ProjectionConsumerMetrics.SaveProjectionAsync(
            _dbContext,
            _logger,
            ProjectionName,
            nameof(ShipmentDispatchedEvent),
            message.ShipmentId,
            message.Timestamp,
            message.CorrelationId,
            context.CancellationToken);
    }
}

public sealed class ShipmentSummaryConsumer :
    IConsumer<ShipmentPackedEvent>,
    IConsumer<ShipmentDispatchedEvent>
{
    private const string ProjectionName = "ShipmentSummary";
    private readonly WarehouseDbContext _dbContext;
    private readonly ILogger<ShipmentSummaryConsumer> _logger;

    public ShipmentSummaryConsumer(
        WarehouseDbContext dbContext,
        ILogger<ShipmentSummaryConsumer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ShipmentPackedEvent> context)
    {
        var message = context.Message;
        if (!await ProjectionConsumerMetrics.TryRegisterEventAsync(
                _dbContext,
                nameof(ShipmentSummaryConsumer),
                message.EventId,
                context.CancellationToken))
        {
            return;
        }

        var summary = await _dbContext.ShipmentSummaries
            .FirstOrDefaultAsync(x => x.Id == message.ShipmentId, context.CancellationToken);

        if (summary is null)
        {
            summary = new ShipmentSummary { Id = message.ShipmentId };
            _dbContext.ShipmentSummaries.Add(summary);
        }

        summary.ShipmentNumber = message.ShipmentNumber;
        summary.OutboundOrderId = message.OutboundOrderId;
        summary.OutboundOrderNumber = message.OutboundOrderNumber;
        summary.Status = "PACKED";
        summary.PackedAt = message.PackedAt;
        summary.PackedBy = message.PackedBy;
        summary.Carrier = "OTHER";

        await ProjectionConsumerMetrics.SaveProjectionAsync(
            _dbContext,
            _logger,
            ProjectionName,
            nameof(ShipmentPackedEvent),
            message.ShipmentId,
            message.Timestamp,
            message.CorrelationId,
            context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<ShipmentDispatchedEvent> context)
    {
        var message = context.Message;
        if (!await ProjectionConsumerMetrics.TryRegisterEventAsync(
                _dbContext,
                nameof(ShipmentSummaryConsumer),
                message.EventId,
                context.CancellationToken))
        {
            return;
        }

        var summary = await _dbContext.ShipmentSummaries
            .FirstOrDefaultAsync(x => x.Id == message.ShipmentId, context.CancellationToken);

        if (summary is null)
        {
            await ProjectionConsumerMetrics.SaveProjectionAsync(
                _dbContext,
                _logger,
                ProjectionName,
                nameof(ShipmentDispatchedEvent),
                message.ShipmentId,
                message.Timestamp,
                message.CorrelationId,
                context.CancellationToken);
            return;
        }

        summary.Status = "DISPATCHED";
        summary.Carrier = message.Carrier;
        summary.TrackingNumber = message.TrackingNumber;
        summary.DispatchedAt = message.DispatchedAt;
        summary.DispatchedBy = message.DispatchedBy;

        await ProjectionConsumerMetrics.SaveProjectionAsync(
            _dbContext,
            _logger,
            ProjectionName,
            nameof(ShipmentDispatchedEvent),
            message.ShipmentId,
            message.Timestamp,
            message.CorrelationId,
            context.CancellationToken);
    }
}

public sealed class DispatchHistoryConsumer : IConsumer<ShipmentDispatchedEvent>
{
    private const string ProjectionName = "DispatchHistory";
    private readonly WarehouseDbContext _dbContext;
    private readonly ILogger<DispatchHistoryConsumer> _logger;

    public DispatchHistoryConsumer(
        WarehouseDbContext dbContext,
        ILogger<DispatchHistoryConsumer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ShipmentDispatchedEvent> context)
    {
        var message = context.Message;
        if (!await ProjectionConsumerMetrics.TryRegisterEventAsync(
                _dbContext,
                nameof(DispatchHistoryConsumer),
                message.EventId,
                context.CancellationToken))
        {
            return;
        }

        var history = new DispatchHistory
        {
            ShipmentId = message.ShipmentId,
            ShipmentNumber = message.ShipmentNumber,
            OutboundOrderNumber = message.OutboundOrderNumber,
            Carrier = message.Carrier,
            TrackingNumber = message.TrackingNumber,
            VehicleId = message.VehicleId,
            DispatchedAt = message.DispatchedAt,
            DispatchedBy = message.DispatchedBy,
            ManualTracking = message.ManualTracking
        };

        _dbContext.DispatchHistories.Add(history);
        await ProjectionConsumerMetrics.SaveProjectionAsync(
            _dbContext,
            _logger,
            ProjectionName,
            nameof(ShipmentDispatchedEvent),
            message.ShipmentId,
            message.Timestamp,
            message.CorrelationId,
            context.CancellationToken);
    }
}

internal static class ProjectionConsumerMetrics
{
    private static readonly Meter ProjectionMeter = new("LKvitai.MES.Projections.Outbound");
    private static readonly Counter<long> ProjectionUpdatesTotal =
        ProjectionMeter.CreateCounter<long>("projection_updates_total");
    private static readonly Histogram<double> ProjectionUpdateDurationMs =
        ProjectionMeter.CreateHistogram<double>("projection_update_duration_ms");
    private static readonly Histogram<double> ProjectionLagSeconds =
        ProjectionMeter.CreateHistogram<double>("projection_lag_seconds");
    private static readonly Counter<long> ProjectionErrorsTotal =
        ProjectionMeter.CreateCounter<long>("projection_errors_total");

    public static async Task<bool> TryRegisterEventAsync(
        WarehouseDbContext dbContext,
        string handlerName,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var streamId = eventId == Guid.Empty ? Guid.NewGuid().ToString("N") : eventId.ToString("N");
        var alreadyProcessed = await dbContext.EventProcessingCheckpoints
            .AsNoTracking()
            .AnyAsync(
                x => x.HandlerName == handlerName && x.StreamId == streamId,
                cancellationToken);

        if (alreadyProcessed)
        {
            return false;
        }

        dbContext.EventProcessingCheckpoints.Add(new EventProcessingCheckpoint
        {
            HandlerName = handlerName,
            StreamId = streamId,
            LastEventNumber = 1,
            ProcessedAt = DateTimeOffset.UtcNow
        });

        return true;
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
                new KeyValuePair<string, object?>("projection_name", projectionName),
                new KeyValuePair<string, object?>("error_type", ex.GetType().Name));

            logger.LogError(
                ex,
                "Projection update failed for {ProjectionName} and event {EventType}, entity {EntityId}",
                projectionName,
                eventType,
                entityId);
            throw;
        }

        var durationMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        var lagSeconds = Math.Max(0d, (DateTime.UtcNow - DateTime.SpecifyKind(eventTimestampUtc, DateTimeKind.Utc)).TotalSeconds);

        ProjectionUpdatesTotal.Add(
            1,
            new KeyValuePair<string, object?>("projection_name", projectionName),
            new KeyValuePair<string, object?>("event_type", eventType));

        ProjectionUpdateDurationMs.Record(
            durationMs,
            new KeyValuePair<string, object?>("projection_name", projectionName));

        ProjectionLagSeconds.Record(
            lagSeconds,
            new KeyValuePair<string, object?>("projection_name", projectionName));

        logger.LogInformation(
            "Projection {ProjectionName} updated for event {EventType}, entity {EntityId}, lag {LagSeconds:F3}s, correlation {CorrelationId}",
            projectionName,
            eventType,
            entityId,
            lagSeconds,
            correlationId ?? string.Empty);

        if (lagSeconds > 5d)
        {
            logger.LogWarning(
                "Projection lag high: {LagSeconds:F3}s for {ProjectionName}",
                lagSeconds,
                projectionName);
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
