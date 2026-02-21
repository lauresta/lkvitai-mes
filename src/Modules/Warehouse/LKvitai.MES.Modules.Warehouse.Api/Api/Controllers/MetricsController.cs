using System.Globalization;
using System.Text;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Caching;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Route("metrics")]
[AllowAnonymous]
public sealed class MetricsController : ControllerBase
{
    private readonly ICacheService _cacheService;
    private readonly ISlaMonitoringService _slaMonitoringService;
    private readonly ICapacityPlanningService _capacityPlanningService;
    private readonly int _minPoolSize;
    private readonly int _maxPoolSize;

    public MetricsController(
        ICacheService cacheService,
        ISlaMonitoringService slaMonitoringService,
        ICapacityPlanningService capacityPlanningService,
        IConfiguration configuration)
    {
        _cacheService = cacheService;
        _slaMonitoringService = slaMonitoringService;
        _capacityPlanningService = capacityPlanningService;
        var connectionString = configuration.GetConnectionString("WarehouseDb") ?? string.Empty;
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        _minPoolSize = builder.MinPoolSize <= 0 ? 10 : builder.MinPoolSize;
        _maxPoolSize = builder.MaxPoolSize <= 0 ? 100 : builder.MaxPoolSize;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var m = _cacheService.GetMetrics();
        var sla = await _slaMonitoringService.GetSnapshotAsync(cancellationToken);
        var capacity = await _capacityPlanningService.GetSnapshotAsync(cancellationToken);

        var builder = new StringBuilder();
        builder.AppendLine("# TYPE cache_hit_rate gauge");
        builder.AppendLine($"cache_hit_rate {m.HitRate.ToString("F4", CultureInfo.InvariantCulture)}");
        builder.AppendLine("# TYPE cache_miss_total counter");
        builder.AppendLine($"cache_miss_total {m.Misses}");
        builder.AppendLine("# TYPE cache_hit_total counter");
        builder.AppendLine($"cache_hit_total {m.Hits}");
        builder.AppendLine("# TYPE cache_latency_ms gauge");
        builder.AppendLine($"cache_latency_ms {m.AverageLatencyMs.ToString("F4", CultureInfo.InvariantCulture)}");
        builder.AppendLine("# TYPE cache_size gauge");
        builder.AppendLine($"cache_size {m.TrackedKeyCount}");

        var pool = ConnectionPoolMetrics.Snapshot(_minPoolSize, _maxPoolSize);
        builder.AppendLine("# TYPE npgsql_connection_active gauge");
        builder.AppendLine($"npgsql_connection_active {pool.ActiveConnections}");
        builder.AppendLine("# TYPE npgsql_connection_idle gauge");
        builder.AppendLine($"npgsql_connection_idle {pool.IdleConnections}");
        builder.AppendLine("# TYPE npgsql_connection_wait_ms gauge");
        builder.AppendLine($"npgsql_connection_wait_ms {pool.AvgConnectionWaitMs.ToString("F4", CultureInfo.InvariantCulture)}");
        builder.AppendLine("# TYPE npgsql_connection_errors_total counter");
        builder.AppendLine($"npgsql_connection_errors_total {pool.ConnectionErrors}");
        builder.AppendLine("# TYPE npgsql_connection_pool_min gauge");
        builder.AppendLine($"npgsql_connection_pool_min {pool.MinimumPoolSize}");
        builder.AppendLine("# TYPE npgsql_connection_pool_max gauge");
        builder.AppendLine($"npgsql_connection_pool_max {pool.MaximumPoolSize}");
        builder.AppendLine("# TYPE sla_uptime_percentage gauge");
        builder.AppendLine($"sla_uptime_percentage {sla.UptimePercentage.ToString("F4", CultureInfo.InvariantCulture)}");
        builder.AppendLine("# TYPE sla_api_response_time_p95 gauge");
        builder.AppendLine($"sla_api_response_time_p95 {sla.ApiResponseTimeP95Ms.ToString("F4", CultureInfo.InvariantCulture)}");
        builder.AppendLine("# TYPE sla_projection_lag_seconds gauge");
        builder.AppendLine($"sla_projection_lag_seconds {sla.ProjectionLagSeconds.ToString("F4", CultureInfo.InvariantCulture)}");
        builder.AppendLine("# TYPE sla_order_fulfillment_rate gauge");
        builder.AppendLine($"sla_order_fulfillment_rate {sla.OrderFulfillmentRate.ToString("F4", CultureInfo.InvariantCulture)}");
        builder.AppendLine("# TYPE capacity_database_size_gb gauge");
        builder.AppendLine($"capacity_database_size_gb {capacity.DatabaseSizeGb.ToString("F4", CultureInfo.InvariantCulture)}");
        builder.AppendLine("# TYPE capacity_event_count gauge");
        builder.AppendLine($"capacity_event_count {capacity.EventsPerDay.ToString("F4", CultureInfo.InvariantCulture)}");
        builder.AppendLine("# TYPE capacity_api_request_volume_per_hour gauge");
        builder.AppendLine($"capacity_api_request_volume_per_hour {capacity.ApiRequestsPerHour.ToString("F4", CultureInfo.InvariantCulture)}");
        builder.AppendLine("# TYPE capacity_location_utilization_percent gauge");
        builder.AppendLine($"capacity_location_utilization_percent {capacity.LocationUtilizationPercent.ToString("F4", CultureInfo.InvariantCulture)}");

        return Content(builder.ToString(), "text/plain; version=0.0.4");
    }
}
