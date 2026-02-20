using LKvitai.MES.Modules.Warehouse.Api.Observability;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Integration;

public sealed class CapacityPlanningTests
{
    [Fact]
    public void AppSettings_ShouldContainCapacityPlanningConfiguration()
    {
        var appsettings = File.ReadAllText(ApiPathResolver.ResolveApiFileOrFail("appsettings.json"));

        Assert.Contains("\"CapacityPlanning\"", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"AllocatedDatabaseStorageGb\"", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"DatabaseGrowthPerMonthGb\"", appsettings, StringComparison.Ordinal);
    }

    [Fact]
    public void MetricsController_ShouldExposeCapacityMetrics()
    {
        var controller = File.ReadAllText(ApiPathResolver.ResolveApiFileOrFail("Api", "Controllers", "MetricsController.cs"));

        Assert.Contains("capacity_database_size_gb", controller, StringComparison.Ordinal);
        Assert.Contains("capacity_event_count", controller, StringComparison.Ordinal);
        Assert.Contains("capacity_location_utilization_percent", controller, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildReport_ShouldReturnForecastsAndRecommendations()
    {
        await using var db = CreateDbContext();
        db.Locations.AddRange(
            new Location { Id = 1, Code = "A-01", Barcode = "A-01", Type = "Bin", Status = "Occupied" },
            new Location { Id = 2, Code = "A-02", Barcode = "A-02", Type = "Bin", Status = "Active" });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var report = await service.BuildReportAsync(CancellationToken.None);

        Assert.True(report.Forecasts.DatabaseSizeGbAt6Months >= report.Current.DatabaseSizeGb);
        Assert.True(report.Forecasts.EventsPerDayAt6Months > report.Current.EventsPerDay);
        Assert.NotNull(report.Recommendations);
    }

    [Fact]
    public void SimulateAlert_ShouldTriggerLocationAlertAtNinetyPercent()
    {
        using var db = CreateDbContext();
        var service = CreateService(db);

        var alert = service.SimulateAlert("location", 92);

        Assert.Equal("HighLocationUtilization", alert.AlertName);
        Assert.True(alert.Triggered);
    }

    private static CapacityPlanningService CreateService(WarehouseDbContext dbContext)
    {
        var requestStore = new SlaRequestMetricsStore(Options.Create(new SlaMonitoringOptions()));
        requestStore.AddRequestDuration(200);
        requestStore.AddRequestDuration(300);

        return new CapacityPlanningService(
            dbContext,
            requestStore,
            Options.Create(new CapacityPlanningOptions
            {
                AllocatedDatabaseStorageGb = 150,
                DatabaseGrowthPerMonthGb = 5,
                CurrentEventsPerDay = 10_000,
                EventGrowthPerDay = 83.3,
                ForecastMonths = 6,
                DatabaseWarningPercent = 80,
                LocationCriticalPercent = 90,
                EventWarningPerDay = 1_000_000
            }));
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"capacity-tests-{Guid.NewGuid():N}")
            .Options;

        return new WarehouseDbContext(options);
    }

}
