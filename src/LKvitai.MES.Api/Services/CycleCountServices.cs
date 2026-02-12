using System.Diagnostics.Metrics;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Application.Commands;
using LKvitai.MES.Application.Ports;
using LKvitai.MES.Application.Services;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using Marten;
using MediatR;
using Microsoft.EntityFrameworkCore;
using IDocumentStore = Marten.IDocumentStore;

namespace LKvitai.MES.Api.Services;

public interface ICycleCountQuantityResolver
{
    Task<decimal> ResolveSystemQtyAsync(string locationCode, string sku, CancellationToken cancellationToken = default);
}

public sealed class MartenCycleCountQuantityResolver : ICycleCountQuantityResolver
{
    private const string DefaultWarehouseId = "WH1";
    private readonly IDocumentStore _documentStore;

    public MartenCycleCountQuantityResolver(IDocumentStore documentStore)
    {
        _documentStore = documentStore;
    }

    public async Task<decimal> ResolveSystemQtyAsync(
        string locationCode,
        string sku,
        CancellationToken cancellationToken = default)
    {
        await using var querySession = _documentStore.QuerySession();
        var query = querySession.Query<AvailableStockView>()
            .Where(x => x.WarehouseId == DefaultWarehouseId && x.Location == locationCode && x.SKU == sku);

        return await Marten.QueryableExtensions.SumAsync(query, x => x.OnHandQty, cancellationToken);
    }
}

internal static class CycleCountMetrics
{
    private static readonly Meter Meter = new("LKvitai.MES.CycleCounts");
    private static double _accuracyPercentage = 100d;

    public static readonly Counter<long> ScheduledTotal =
        Meter.CreateCounter<long>("cycle_counts_scheduled_total");
    public static readonly Counter<long> CompletedTotal =
        Meter.CreateCounter<long>("cycle_counts_completed_total");
    public static readonly Counter<long> DiscrepanciesTotal =
        Meter.CreateCounter<long>("cycle_count_discrepancies_total");

    private static readonly ObservableGauge<double> AccuracyGauge =
        Meter.CreateObservableGauge(
            "cycle_count_accuracy_percentage",
            () => new Measurement<double>(_accuracyPercentage));

    public static void SetAccuracy(double accuracyPercentage)
    {
        _accuracyPercentage = accuracyPercentage;
    }
}

public sealed class ScheduleCycleCountCommandHandler : IRequestHandler<ScheduleCycleCountCommand, Result>
{
    private readonly WarehouseDbContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly ICycleCountQuantityResolver _quantityResolver;
    private readonly ILogger<ScheduleCycleCountCommandHandler> _logger;

    public ScheduleCycleCountCommandHandler(
        WarehouseDbContext dbContext,
        IEventBus eventBus,
        ICycleCountQuantityResolver quantityResolver,
        ILogger<ScheduleCycleCountCommandHandler> logger)
    {
        _dbContext = dbContext;
        _eventBus = eventBus;
        _quantityResolver = quantityResolver;
        _logger = logger;
    }

    public async Task<Result> Handle(ScheduleCycleCountCommand request, CancellationToken cancellationToken)
    {
        var scheduledDate = request.ScheduledDate.Date;
        if (scheduledDate < DateTimeOffset.UtcNow.Date)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Scheduled date must be >= today.");
        }

        if (string.IsNullOrWhiteSpace(request.AssignedOperator))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "AssignedOperator is required.");
        }

        var normalizedAbcClass = string.IsNullOrWhiteSpace(request.AbcClass)
            ? "ALL"
            : request.AbcClass.Trim().ToUpperInvariant();
        if (normalizedAbcClass is not ("A" or "B" or "C" or "ALL"))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "ABCClass must be one of: A, B, C, ALL.");
        }

        var items = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            _dbContext.Items
                .AsNoTracking()
                .Include(x => x.Category)
                .Where(x => x.Status == "Active")
                .OrderBy(x => x.Id),
            cancellationToken);

        if (items.Count == 0)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "No active items found for cycle count scheduling.");
        }

        var filteredItems = normalizedAbcClass == "ALL"
            ? items
            : items.Where(x =>
                    x.Category?.Code.StartsWith(normalizedAbcClass, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
        if (filteredItems.Count == 0)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"No active items found for ABC class '{normalizedAbcClass}'.");
        }

        var locationQuery = _dbContext.Locations
            .AsNoTracking()
            .Where(x => x.Status == "Active" && !x.IsVirtual);
        if (request.LocationIds.Count > 0)
        {
            var locationIdSet = request.LocationIds.Distinct().ToArray();
            locationQuery = locationQuery.Where(x => locationIdSet.Contains(x.Id));
        }

        var locations = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            locationQuery
                .OrderBy(x => x.Id),
            cancellationToken);

        if (locations.Count == 0)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "At least one active location is required.");
        }

        var dayStart = new DateTimeOffset(scheduledDate, TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);
        var existingForDay = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(
            _dbContext.CycleCounts
                .AsNoTracking()
                .Where(x => x.ScheduledDate >= dayStart && x.ScheduledDate < dayEnd),
            cancellationToken);

        var cycleCount = new CycleCount
        {
            CountNumber = $"CC-{scheduledDate:yyyyMMdd}-{existingForDay + 1:000}",
            ScheduledDate = request.ScheduledDate,
            AbcClass = normalizedAbcClass,
            AssignedOperator = request.AssignedOperator.Trim(),
            ScheduleCommandId = request.CommandId
        };

        for (var i = 0; i < locations.Count; i++)
        {
            var location = locations[i];
            var item = filteredItems[i % filteredItems.Count];
            var systemQty = await _quantityResolver.ResolveSystemQtyAsync(location.Code, item.InternalSKU, cancellationToken);

            cycleCount.Lines.Add(new CycleCountLine
            {
                ItemId = item.Id,
                LocationId = location.Id,
                SystemQty = systemQty,
                PhysicalQty = 0m,
                Delta = 0m,
                Status = CycleCountLineStatus.Pending
            });
        }

        _dbContext.CycleCounts.Add(cycleCount);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new CycleCountScheduledEvent
        {
            CycleCountId = cycleCount.Id,
            CountNumber = cycleCount.CountNumber,
            ScheduledDate = cycleCount.ScheduledDate.UtcDateTime,
            LineCount = cycleCount.Lines.Count
        }, cancellationToken);

        CycleCountMetrics.ScheduledTotal.Add(1);
        _logger.LogInformation(
            "Cycle count scheduled: {CountNumber}, {LineCount} lines",
            cycleCount.CountNumber,
            cycleCount.Lines.Count);

        return Result.Ok();
    }
}

public sealed class RecordCountCommandHandler : IRequestHandler<RecordCountCommand, Result>
{
    private readonly WarehouseDbContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<RecordCountCommandHandler> _logger;

    public RecordCountCommandHandler(
        WarehouseDbContext dbContext,
        IEventBus eventBus,
        ICurrentUserService currentUserService,
        ILogger<RecordCountCommandHandler> logger)
    {
        _dbContext = dbContext;
        _eventBus = eventBus;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result> Handle(RecordCountCommand request, CancellationToken cancellationToken)
    {
        if (request.PhysicalQty < 0m)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "PhysicalQty cannot be negative.");
        }

        var cycleCountQuery = _dbContext.CycleCounts
            .Include(x => x.Lines)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Item)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Location);
        var cycleCount = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            cycleCountQuery,
            x => x.Id == request.CycleCountId,
            cancellationToken);
        if (cycleCount is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, "Cycle count not found.");
        }

        var line = cycleCount.Lines.FirstOrDefault(x => x.LocationId == request.LocationId && x.ItemId == request.ItemId);
        if (line is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, "Cycle count line not found for location and item.");
        }

        var countedBy = string.IsNullOrWhiteSpace(request.CountedBy)
            ? _currentUserService.GetCurrentUserId()
            : request.CountedBy.Trim();
        var startResult = cycleCount.Start(countedBy, request.CommandId, DateTimeOffset.UtcNow);
        if (!startResult.IsSuccess)
        {
            return startResult;
        }

        line.PhysicalQty = request.PhysicalQty;
        line.Delta = decimal.Round(line.PhysicalQty - line.SystemQty, 3, MidpointRounding.AwayFromZero);
        line.Status = CycleCountLineStatus.Pending;
        line.CountedAt = DateTimeOffset.UtcNow;
        line.CountedBy = countedBy;
        line.Reason = request.Reason;

        if (cycleCount.Lines.All(x => x.CountedAt.HasValue))
        {
            var completeResult = cycleCount.MarkCompleted(DateTimeOffset.UtcNow);
            if (!completeResult.IsSuccess)
            {
                return completeResult;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new CountRecordedEvent
        {
            CycleCountId = cycleCount.Id,
            CountNumber = cycleCount.CountNumber,
            LocationId = line.LocationId,
            ItemId = line.ItemId,
            SystemQty = line.SystemQty,
            PhysicalQty = line.PhysicalQty,
            Delta = line.Delta,
            CountedBy = countedBy,
            RecordedAt = DateTime.UtcNow
        }, cancellationToken);

        _logger.LogInformation(
            "Count recorded: {LocationCode}, {ItemSku}, Delta {Delta}",
            line.Location?.Code ?? line.LocationId.ToString(),
            line.Item?.InternalSKU ?? line.ItemId.ToString(),
            line.Delta);

        return Result.Ok();
    }
}

public sealed class ApplyAdjustmentCommandHandler : IRequestHandler<ApplyAdjustmentCommand, Result>
{
    private const string DefaultWarehouseId = "WH1";

    private readonly WarehouseDbContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ApplyAdjustmentCommandHandler> _logger;

    public ApplyAdjustmentCommandHandler(
        WarehouseDbContext dbContext,
        IEventBus eventBus,
        ICurrentUserService currentUserService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ApplyAdjustmentCommandHandler> logger)
    {
        _dbContext = dbContext;
        _eventBus = eventBus;
        _currentUserService = currentUserService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<Result> Handle(ApplyAdjustmentCommand request, CancellationToken cancellationToken)
    {
        var cycleCountQuery = _dbContext.CycleCounts
            .Include(x => x.Lines)
                .ThenInclude(x => x.Item)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Location);
        var cycleCount = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            cycleCountQuery,
            x => x.Id == request.CycleCountId,
            cancellationToken);
        if (cycleCount is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, "Cycle count not found.");
        }

        if (cycleCount.Lines.Count == 0)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Cycle count has no lines.");
        }

        var currentUserId = _currentUserService.GetCurrentUserId();
        var hasManagerRole = HasManagerRole();
        var unitCostLookup = await LoadUnitCostLookupAsync(cycleCount.Lines.Select(x => x.ItemId).Distinct().ToArray(), cancellationToken);

        var approvedLines = 0;
        var discrepancyLines = 0;
        foreach (var line in cycleCount.Lines)
        {
            line.Delta = decimal.Round(line.PhysicalQty - line.SystemQty, 3, MidpointRounding.AwayFromZero);
            if (line.Delta == 0m)
            {
                line.Status = CycleCountLineStatus.Approved;
                approvedLines++;
                continue;
            }

            var absoluteDelta = Math.Abs(line.Delta);
            var discrepancyPercent = line.SystemQty == 0m
                ? 100m
                : (absoluteDelta / line.SystemQty) * 100m;

            unitCostLookup.TryGetValue(line.ItemId, out var unitCost);
            var impact = Math.Abs(line.Delta * unitCost);
            var requiresApproval = discrepancyPercent > 5m || impact > 1000m;

            if (requiresApproval && !hasManagerRole)
            {
                return Result.Fail(
                    DomainErrorCodes.ValidationError,
                    "Manager approval required for discrepancy > 5%");
            }

            if (requiresApproval)
            {
                discrepancyLines++;
                _logger.LogWarning(
                    "Discrepancy requires approval: Delta {Delta}, Impact ${Impact}",
                    line.Delta,
                    impact);
            }

            await _eventBus.PublishAsync(new StockAdjustedEvent
            {
                AggregateId = cycleCount.Id,
                UserId = currentUserId,
                WarehouseId = DefaultWarehouseId,
                AdjustmentId = Guid.NewGuid(),
                ItemId = line.ItemId,
                SKU = line.Item?.InternalSKU ?? string.Empty,
                LocationId = line.LocationId,
                Location = line.Location?.Code ?? line.LocationId.ToString(),
                QtyDelta = line.Delta,
                ReasonCode = "CYCLE_COUNT",
                Notes = line.Reason,
                Timestamp = DateTime.UtcNow
            }, cancellationToken);

            line.Status = CycleCountLineStatus.Approved;
            approvedLines++;

            CycleCountMetrics.DiscrepanciesTotal.Add(
                1,
                new KeyValuePair<string, object?>("approval_required", requiresApproval));
        }

        var approvedBy = string.IsNullOrWhiteSpace(request.ApproverId)
            ? currentUserId
            : request.ApproverId.Trim();
        var completeResult = cycleCount.Complete(approvedBy, request.CommandId, DateTimeOffset.UtcNow);
        if (!completeResult.IsSuccess)
        {
            return completeResult;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var accuracy = cycleCount.Lines.Count == 0
            ? 100d
            : ((cycleCount.Lines.Count - discrepancyLines) / (double)cycleCount.Lines.Count) * 100d;

        CycleCountMetrics.SetAccuracy(accuracy);
        CycleCountMetrics.CompletedTotal.Add(1);

        await _eventBus.PublishAsync(new CycleCountCompletedEvent
        {
            CycleCountId = cycleCount.Id,
            CountNumber = cycleCount.CountNumber,
            ApprovedLines = approvedLines,
            DiscrepancyLines = discrepancyLines,
            AccuracyPercentage = (decimal)accuracy,
            ApprovedBy = approvedBy,
            CompletedAt = cycleCount.CompletedAt?.UtcDateTime ?? DateTime.UtcNow
        }, cancellationToken);

        return Result.Ok();
    }

    private bool HasManagerRole()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user?.IsInRole(WarehouseRoles.WarehouseManager) == true ||
               user?.IsInRole(WarehouseRoles.WarehouseAdmin) == true;
    }

    private async Task<Dictionary<int, decimal>> LoadUnitCostLookupAsync(
        IReadOnlyCollection<int> itemIds,
        CancellationToken cancellationToken)
    {
        if (itemIds.Count == 0)
        {
            return new Dictionary<int, decimal>();
        }

        var values = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            _dbContext.OnHandValues
                .AsNoTracking()
                .Where(x => itemIds.Contains(x.ItemId))
                .Select(x => new { x.ItemId, x.UnitCost }),
            cancellationToken);

        return values
            .GroupBy(x => x.ItemId)
            .ToDictionary(x => x.Key, x => x.Last().UnitCost);
    }
}
