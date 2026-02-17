using LKvitai.MES.Api.Observability;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public sealed class SLAMonitoringTests
{
    [Fact]
    public void AppSettings_ShouldContainSlaMonitoringConfiguration()
    {
        var appsettings = File.ReadAllText(ResolvePathFromRepoRoot("src/LKvitai.MES.Api/appsettings.json"));

        Assert.Contains("\"SlaMonitoring\"", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"UptimeTargetPercent\"", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"ApiP95TargetMs\"", appsettings, StringComparison.Ordinal);
        Assert.Contains("\"OrderFulfillmentTargetRate\"", appsettings, StringComparison.Ordinal);
    }

    [Fact]
    public void MetricsController_ShouldExposeSlaMetricNames()
    {
        var controller = File.ReadAllText(ResolvePathFromRepoRoot("src/LKvitai.MES.Api/Api/Controllers/MetricsController.cs"));

        Assert.Contains("sla_uptime_percentage", controller, StringComparison.Ordinal);
        Assert.Contains("sla_api_response_time_p95", controller, StringComparison.Ordinal);
        Assert.Contains("sla_projection_lag_seconds", controller, StringComparison.Ordinal);
        Assert.Contains("sla_order_fulfillment_rate", controller, StringComparison.Ordinal);
    }

    [Fact]
    public void RequestMetricsStore_ShouldReturnP95()
    {
        var store = new SlaRequestMetricsStore(Options.Create(new SlaMonitoringOptions { RequestWindowSize = 100 }));

        foreach (var value in new[] { 100d, 200d, 300d, 400d, 500d, 600d, 700d, 800d, 900d, 1000d })
        {
            store.AddRequestDuration(value);
        }

        var p95 = store.GetP95Milliseconds();
        Assert.Equal(1000d, p95);
    }

    [Fact]
    public async Task Snapshot_ShouldCalculateOrderFulfillmentRate()
    {
        await using var db = CreateDbContext();
        var onTimeOrder = new SalesOrder
        {
            Id = Guid.NewGuid(),
            OrderDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))
        };
        var lateOrder = new SalesOrder
        {
            Id = Guid.NewGuid(),
            OrderDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2))
        };
        SetPrivateShippedAt(onTimeOrder, DateTimeOffset.UtcNow.AddHours(-12));
        SetPrivateShippedAt(lateOrder, DateTimeOffset.UtcNow);
        db.SalesOrders.AddRange(onTimeOrder, lateOrder);
        await db.SaveChangesAsync();

        var store = new SlaRequestMetricsStore(Options.Create(new SlaMonitoringOptions { RequestWindowSize = 100 }));
        store.AddRequestDuration(120);
        store.AddRequestDuration(480);

        var service = new SlaMonitoringService(
            db,
            store,
            Options.Create(new SlaMonitoringOptions
            {
                TrackingWindowDays = 30,
                UptimeTargetPercent = 99.9,
                ApiP95TargetMs = 500,
                ProjectionLagTargetSeconds = 1,
                OrderFulfillmentTargetRate = 0.95
            }));

        var snapshot = await service.GetSnapshotAsync(CancellationToken.None);

        Assert.InRange(snapshot.OrderFulfillmentRate, 0, 1);
    }

    [Fact]
    public async Task BuildMonthlyReport_ShouldIncludeBreachSummary()
    {
        await using var db = CreateDbContext();
        var store = new SlaRequestMetricsStore(Options.Create(new SlaMonitoringOptions { RequestWindowSize = 100 }));
        store.AddRequestDuration(900);
        store.AddRequestDuration(950);

        var service = new SlaMonitoringService(
            db,
            store,
            Options.Create(new SlaMonitoringOptions
            {
                ApiP95TargetMs = 500,
                UptimeTargetPercent = 99.9,
                ProjectionLagTargetSeconds = 1,
                OrderFulfillmentTargetRate = 0.95
            }));

        var report = await service.BuildMonthlyReportAsync(new DateOnly(2026, 1, 1), CancellationToken.None);

        Assert.Contains("SLA Report for 2026-01", report.ReportBody, StringComparison.Ordinal);
        Assert.Contains("Breach incidents:", report.ReportBody, StringComparison.Ordinal);
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"sla-tests-{Guid.NewGuid():N}")
            .Options;

        return new WarehouseDbContext(options);
    }

    private static void SetPrivateShippedAt(SalesOrder order, DateTimeOffset value)
    {
        var property = typeof(SalesOrder).GetProperty(nameof(SalesOrder.ShippedAt));
        property!.SetValue(order, value);
    }

    private static string ResolvePathFromRepoRoot(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return Path.Combine(directory.FullName, relativePath);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate repository root (.git) from test runtime directory.");
    }
}
