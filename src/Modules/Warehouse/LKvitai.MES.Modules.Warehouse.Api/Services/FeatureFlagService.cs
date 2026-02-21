using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public sealed class FeatureFlagsOptions
{
    public const string SectionName = "FeatureFlags";

    public bool Enable3DVisualization { get; set; }
    public bool EnableWavePicking { get; set; }
    public bool EnableAgnumExport { get; set; } = true;
    public int MaxOrderLines { get; set; } = 100;
    public int AgnumExportRolloutPercent { get; set; } = 100;
    public int CacheTtlSeconds { get; set; } = 30;
    public string[] WavePickingTargetUsers { get; set; } = Array.Empty<string>();
    public string[] WavePickingTargetRoles { get; set; } = Array.Empty<string>();
}

public sealed record FeatureFlagEvaluation(string FlagKey, bool Enabled, int? NumericValue = null);

public interface IFeatureFlagService
{
    FeatureFlagEvaluation Evaluate(string flagKey, ClaimsPrincipal principal, string? userOverride = null);
}

public sealed class FeatureFlagService : IFeatureFlagService
{
    private readonly IMemoryCache _cache;
    private readonly IOptionsMonitor<FeatureFlagsOptions> _options;

    public FeatureFlagService(IMemoryCache cache, IOptionsMonitor<FeatureFlagsOptions> options)
    {
        _cache = cache;
        _options = options;
    }

    public FeatureFlagEvaluation Evaluate(string flagKey, ClaimsPrincipal principal, string? userOverride = null)
    {
        var userId = string.IsNullOrWhiteSpace(userOverride)
            ? principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous"
            : userOverride;

        var roles = principal.Claims
            .Where(x => x.Type == ClaimTypes.Role)
            .Select(x => x.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var cacheKey = $"feature:{flagKey}:{userId}:{string.Join(',', roles.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))}";
        var ttl = Math.Max(1, _options.CurrentValue.CacheTtlSeconds);
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttl);
            return EvaluateInternal(flagKey, userId, roles);
        })!;
    }

    private FeatureFlagEvaluation EvaluateInternal(string flagKey, string userId, HashSet<string> roles)
    {
        var options = _options.CurrentValue;
        return flagKey.ToLowerInvariant() switch
        {
            "enable_3d_visualization" => new FeatureFlagEvaluation(flagKey, options.Enable3DVisualization),
            "enable_wave_picking" => new FeatureFlagEvaluation(
                flagKey,
                options.EnableWavePicking ||
                options.WavePickingTargetUsers.Contains(userId, StringComparer.OrdinalIgnoreCase) ||
                roles.Overlaps(options.WavePickingTargetRoles)),
            "enable_agnum_export" => new FeatureFlagEvaluation(
                flagKey,
                options.EnableAgnumExport && IsInRollout(options.AgnumExportRolloutPercent, userId)),
            "max_order_lines" => new FeatureFlagEvaluation(flagKey, true, options.MaxOrderLines),
            _ => new FeatureFlagEvaluation(flagKey, false)
        };
    }

    private static bool IsInRollout(int percentage, string userId)
    {
        var normalized = Math.Clamp(percentage, 0, 100);
        var bucket = Math.Abs(userId.GetHashCode(StringComparison.Ordinal)) % 100;
        return bucket < normalized;
    }
}
