using LKvitai.MES.Modules.Warehouse.Api.Observability;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public interface ISlaMonitoringService
{
    Task<SlaSnapshot> GetSnapshotAsync(CancellationToken cancellationToken);

    Task<SlaReportResult> BuildMonthlyReportAsync(DateOnly month, CancellationToken cancellationToken);
}

public sealed class SlaMonitoringService : ISlaMonitoringService
{
    private readonly WarehouseDbContext _dbContext;
    private readonly SlaRequestMetricsStore _requestMetricsStore;
    private readonly SlaMonitoringOptions _options;

    public SlaMonitoringService(
        WarehouseDbContext dbContext,
        SlaRequestMetricsStore requestMetricsStore,
        IOptions<SlaMonitoringOptions> options)
    {
        _dbContext = dbContext;
        _requestMetricsStore = requestMetricsStore;
        _options = options.Value;
    }

    public async Task<SlaSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var requestP95 = _requestMetricsStore.GetP95Milliseconds();
        var projectionLagSeconds = await GetProjectionLagSecondsAsync(now, cancellationToken);
        var fulfillment = await GetOrderFulfillmentRateAsync(now, cancellationToken);
        var uptime = CalculateUptime(now);

        return new SlaSnapshot(
            uptime,
            requestP95,
            projectionLagSeconds,
            fulfillment);
    }

    public async Task<SlaReportResult> BuildMonthlyReportAsync(DateOnly month, CancellationToken cancellationToken)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);

        var breaches = 0;
        if (snapshot.UptimePercentage < _options.UptimeTargetPercent)
        {
            breaches++;
        }
        if (snapshot.ApiResponseTimeP95Ms > _options.ApiP95TargetMs)
        {
            breaches++;
        }
        if (snapshot.ProjectionLagSeconds > _options.ProjectionLagTargetSeconds)
        {
            breaches++;
        }
        if (snapshot.OrderFulfillmentRate < _options.OrderFulfillmentTargetRate)
        {
            breaches++;
        }

        var lines = new[]
        {
            $"SLA Report for {month:yyyy-MM}",
            $"GeneratedAt: {DateTimeOffset.UtcNow:O}",
            $"Uptime: {snapshot.UptimePercentage:F2}%",
            $"API p95: {snapshot.ApiResponseTimeP95Ms:F2} ms",
            $"Projection lag: {snapshot.ProjectionLagSeconds:F2} s",
            $"Order fulfillment: {snapshot.OrderFulfillmentRate:P2}",
            $"Breach incidents: {breaches}"
        };

        return new SlaReportResult(
            month,
            snapshot,
            breaches,
            string.Join(Environment.NewLine, lines));
    }

    private double CalculateUptime(DateTimeOffset now)
    {
        var elapsed = now - _requestMetricsStore.StartedAt;
        if (elapsed.TotalMinutes <= 0)
        {
            return 100d;
        }

        var downtimeRatio = Math.Max(0d, _options.PlannedDowntimeMinutes) / elapsed.TotalMinutes;
        var uptime = 100d - (downtimeRatio * 100d);
        return Math.Clamp(uptime, 0d, 100d);
    }

    private async Task<double> GetProjectionLagSecondsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var latestProjectionUpdate = await _dbContext.OnHandValues
            .AsNoTracking()
            .Select(x => (DateTimeOffset?)x.LastUpdated)
            .MaxAsync(cancellationToken);

        if (!latestProjectionUpdate.HasValue)
        {
            return 0d;
        }

        return Math.Max(0d, (now - latestProjectionUpdate.Value).TotalSeconds);
    }

    private async Task<double> GetOrderFulfillmentRateAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var sinceDate = DateOnly.FromDateTime(now.UtcDateTime.AddDays(-Math.Max(1, _options.TrackingWindowDays)));

        var orders = await _dbContext.SalesOrders
            .AsNoTracking()
            .Where(x => x.OrderDate >= sinceDate)
            .Select(x => new { x.OrderDate, x.ShippedAt })
            .ToListAsync(cancellationToken);

        if (orders.Count == 0)
        {
            return 1d;
        }

        var fulfilled = orders.Count(x =>
        {
            if (!x.ShippedAt.HasValue)
            {
                return false;
            }

            var orderStart = new DateTimeOffset(x.OrderDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            return x.ShippedAt.Value <= orderStart.AddHours(24);
        });

        return fulfilled / (double)orders.Count;
    }
}

public sealed class SlaRequestMetricsStore
{
    private readonly object _lock = new();
    private readonly Queue<double> _requestDurationsMs;
    private long _totalRequestCount;

    public SlaRequestMetricsStore(IOptions<SlaMonitoringOptions> options)
    {
        var maxSize = Math.Max(100, options.Value.RequestWindowSize);
        _requestDurationsMs = new Queue<double>(maxSize);
        MaxSize = maxSize;
        StartedAt = DateTimeOffset.UtcNow;
    }

    public int MaxSize { get; }

    public DateTimeOffset StartedAt { get; }

    public void AddRequestDuration(double milliseconds)
    {
        if (milliseconds < 0d)
        {
            return;
        }

        lock (_lock)
        {
            if (_requestDurationsMs.Count >= MaxSize)
            {
                _requestDurationsMs.Dequeue();
            }

            _requestDurationsMs.Enqueue(milliseconds);
            _totalRequestCount++;
        }
    }

    public double GetP95Milliseconds()
    {
        double[] snapshot;
        lock (_lock)
        {
            snapshot = _requestDurationsMs.ToArray();
        }

        if (snapshot.Length == 0)
        {
            return 0d;
        }

        Array.Sort(snapshot);
        var index = (int)Math.Ceiling(snapshot.Length * 0.95) - 1;
        index = Math.Clamp(index, 0, snapshot.Length - 1);
        return snapshot[index];
    }

    public long GetTotalRequestCount()
    {
        lock (_lock)
        {
            return _totalRequestCount;
        }
    }
}

public sealed record SlaSnapshot(
    double UptimePercentage,
    double ApiResponseTimeP95Ms,
    double ProjectionLagSeconds,
    double OrderFulfillmentRate);

public sealed record SlaReportResult(
    DateOnly Month,
    SlaSnapshot Snapshot,
    int BreachIncidents,
    string ReportBody);
