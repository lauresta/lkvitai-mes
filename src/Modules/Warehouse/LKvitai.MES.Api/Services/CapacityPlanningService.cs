using LKvitai.MES.Api.Observability;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LKvitai.MES.Api.Services;

public interface ICapacityPlanningService
{
    Task<CapacitySnapshot> GetSnapshotAsync(CancellationToken cancellationToken);

    Task<CapacityReport> BuildReportAsync(CancellationToken cancellationToken);

    CapacityAlertSimulation SimulateAlert(string type, double utilizationPercent);
}

public sealed class CapacityPlanningService : ICapacityPlanningService
{
    private readonly WarehouseDbContext _dbContext;
    private readonly SlaRequestMetricsStore _requestMetricsStore;
    private readonly CapacityPlanningOptions _options;

    public CapacityPlanningService(
        WarehouseDbContext dbContext,
        SlaRequestMetricsStore requestMetricsStore,
        IOptions<CapacityPlanningOptions> options)
    {
        _dbContext = dbContext;
        _requestMetricsStore = requestMetricsStore;
        _options = options.Value;
    }

    public async Task<CapacitySnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        var dbSize = await EstimateDatabaseSizeGbAsync(cancellationToken);
        var locationUtilization = await ComputeLocationUtilizationAsync(cancellationToken);
        var requestVolumePerHour = ComputeRequestVolumePerHour();
        var eventsPerDay = _options.CurrentEventsPerDay;

        return new CapacitySnapshot(
            dbSize,
            eventsPerDay,
            requestVolumePerHour,
            locationUtilization);
    }

    public async Task<CapacityReport> BuildReportAsync(CancellationToken cancellationToken)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);

        var databaseForecast = snapshot.DatabaseSizeGb + (_options.DatabaseGrowthPerMonthGb * _options.ForecastMonths);
        var eventsPerDayForecast = snapshot.EventsPerDay + (_options.EventGrowthPerDay * (30 * _options.ForecastMonths));
        var dbPercent = _options.AllocatedDatabaseStorageGb <= 0
            ? 0
            : (snapshot.DatabaseSizeGb / _options.AllocatedDatabaseStorageGb) * 100;

        var recommendations = new List<string>();
        if (dbPercent > _options.DatabaseWarningPercent)
        {
            recommendations.Add("Database: upgrade to next tier (storage above 80%).");
        }
        if (snapshot.LocationUtilizationPercent > 85)
        {
            recommendations.Add("Storage: add locations or optimize putaway (utilization above 85%).");
        }
        if (snapshot.ApiRequestsPerHour > 0 && snapshot.ApiRequestsPerHour > 70_000)
        {
            recommendations.Add("API: add 1 instance (CPU sustained proxy above 70%).");
        }

        var alerts = new List<string>();
        if (dbPercent > _options.DatabaseWarningPercent)
        {
            alerts.Add("Warning: Database size above 80% of allocated storage.");
        }
        if (snapshot.LocationUtilizationPercent > _options.LocationCriticalPercent)
        {
            alerts.Add("Critical: Location utilization above 90%.");
        }
        if (snapshot.EventsPerDay > _options.EventWarningPerDay)
        {
            alerts.Add("Warning: Event store throughput above 1M events/day.");
        }

        return new CapacityReport(
            snapshot,
            new CapacityForecast(databaseForecast, eventsPerDayForecast),
            recommendations,
            alerts);
    }

    public CapacityAlertSimulation SimulateAlert(string type, double utilizationPercent)
    {
        var normalized = type.Trim().ToLowerInvariant();
        return normalized switch
        {
            "location" => new CapacityAlertSimulation(
                "HighLocationUtilization",
                utilizationPercent >= _options.LocationCriticalPercent,
                utilizationPercent >= _options.LocationCriticalPercent
                    ? "Add 200 locations or optimize putaway."
                    : "No critical action required."),
            "database" => new CapacityAlertSimulation(
                "HighDatabaseUsage",
                utilizationPercent >= _options.DatabaseWarningPercent,
                utilizationPercent >= _options.DatabaseWarningPercent
                    ? "Upgrade database storage tier."
                    : "No warning threshold reached."),
            _ => new CapacityAlertSimulation("Unknown", false, "Unsupported capacity alert type.")
        };
    }

    private async Task<double> EstimateDatabaseSizeGbAsync(CancellationToken cancellationToken)
    {
        var locationCount = await _dbContext.Locations.CountAsync(cancellationToken);
        var itemCount = await _dbContext.Items.CountAsync(cancellationToken);
        var orderCount = await _dbContext.SalesOrders.CountAsync(cancellationToken);
        var shipmentCount = await _dbContext.Shipments.CountAsync(cancellationToken);

        var estimatedRows = locationCount + itemCount + orderCount + shipmentCount;
        return Math.Round(Math.Max(estimatedRows, 1) * 0.0001, 4);
    }

    private async Task<double> ComputeLocationUtilizationAsync(CancellationToken cancellationToken)
    {
        var totalLocations = await _dbContext.Locations.CountAsync(cancellationToken);
        if (totalLocations == 0)
        {
            return 0d;
        }

        var occupied = await _dbContext.Locations
            .AsNoTracking()
            .Where(x => x.Status == "Occupied")
            .CountAsync(cancellationToken);

        return (occupied / (double)totalLocations) * 100d;
    }

    private double ComputeRequestVolumePerHour()
    {
        var elapsedHours = Math.Max(1d / 60d, (DateTimeOffset.UtcNow - _requestMetricsStore.StartedAt).TotalHours);
        return _requestMetricsStore.GetTotalRequestCount() / elapsedHours;
    }
}

public sealed record CapacitySnapshot(
    double DatabaseSizeGb,
    double EventsPerDay,
    double ApiRequestsPerHour,
    double LocationUtilizationPercent);

public sealed record CapacityForecast(
    double DatabaseSizeGbAt6Months,
    double EventsPerDayAt6Months);

public sealed record CapacityReport(
    CapacitySnapshot Current,
    CapacityForecast Forecasts,
    IReadOnlyCollection<string> Recommendations,
    IReadOnlyCollection<string> Alerts);

public sealed record CapacityAlertSimulation(
    string AlertName,
    bool Triggered,
    string Recommendation);
