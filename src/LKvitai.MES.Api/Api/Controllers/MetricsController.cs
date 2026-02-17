using System.Globalization;
using System.Text;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.Infrastructure.Caching;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("metrics")]
[AllowAnonymous]
public sealed class MetricsController : ControllerBase
{
    private readonly ICacheService _cacheService;
    private readonly int _minPoolSize;
    private readonly int _maxPoolSize;

    public MetricsController(ICacheService cacheService, IConfiguration configuration)
    {
        _cacheService = cacheService;
        var connectionString = configuration.GetConnectionString("WarehouseDb") ?? string.Empty;
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        _minPoolSize = builder.MinPoolSize <= 0 ? 10 : builder.MinPoolSize;
        _maxPoolSize = builder.MaxPoolSize <= 0 ? 100 : builder.MaxPoolSize;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var m = _cacheService.GetMetrics();

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

        return Content(builder.ToString(), "text/plain; version=0.0.4");
    }
}
