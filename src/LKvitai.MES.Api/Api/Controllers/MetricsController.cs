using System.Globalization;
using System.Text;
using LKvitai.MES.Infrastructure.Caching;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("metrics")]
[AllowAnonymous]
public sealed class MetricsController : ControllerBase
{
    private readonly ICacheService _cacheService;

    public MetricsController(ICacheService cacheService)
    {
        _cacheService = cacheService;
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

        return Content(builder.ToString(), "text/plain; version=0.0.4");
    }
}
