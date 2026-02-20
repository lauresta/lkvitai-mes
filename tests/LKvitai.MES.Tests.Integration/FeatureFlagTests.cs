using System.Security.Claims;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public sealed class FeatureFlagTests
{
    [Fact]
    public void Enable3DVisualization_WhenFlagTrue_ShouldReturnEnabled()
    {
        var sut = CreateService(new FeatureFlagsOptions { Enable3DVisualization = true });

        var result = sut.Evaluate("enable_3d_visualization", CreatePrincipal("user-1", "Operator"));

        Assert.True(result.Enabled);
    }

    [Fact]
    public void EnableWavePicking_WhenDisabledAndUserNotTargeted_ShouldReturnDisabled()
    {
        var sut = CreateService(new FeatureFlagsOptions { EnableWavePicking = false });

        var result = sut.Evaluate("enable_wave_picking", CreatePrincipal("user-2", "Operator"));

        Assert.False(result.Enabled);
    }

    [Fact]
    public void EnableWavePicking_WhenUserTargeted_ShouldReturnEnabled()
    {
        var sut = CreateService(new FeatureFlagsOptions
        {
            EnableWavePicking = false,
            WavePickingTargetUsers = ["user-3"]
        });

        var result = sut.Evaluate("enable_wave_picking", CreatePrincipal("user-3", "Operator"));

        Assert.True(result.Enabled);
    }

    [Fact]
    public void EnableAgnumExport_WhenRolloutIsFiftyPercent_ShouldSplitApproximatelyHalf()
    {
        var sut = CreateService(new FeatureFlagsOptions
        {
            EnableAgnumExport = true,
            AgnumExportRolloutPercent = 50
        });

        var users = Enumerable.Range(1, 10_000).Select(i => $"user-{i}").ToArray();

        var enabledCount = users.Count(userId =>
            sut.Evaluate("enable_agnum_export", CreatePrincipal(userId, "Operator")).Enabled);

        var expectedCount = users.Count(userId => IsInExpectedRollout(userId, 50));
        Assert.Equal(expectedCount, enabledCount);
    }

    [Fact]
    public void MaxOrderLines_ShouldReturnNumericFlagValue()
    {
        var sut = CreateService(new FeatureFlagsOptions { MaxOrderLines = 120 });

        var result = sut.Evaluate("max_order_lines", CreatePrincipal("user-4", "WarehouseAdmin"));

        Assert.True(result.Enabled);
        Assert.Equal(120, result.NumericValue);
    }

    private static FeatureFlagService CreateService(FeatureFlagsOptions options)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new FeatureFlagService(cache, new StaticOptionsMonitor<FeatureFlagsOptions>(options));
    }

    private static ClaimsPrincipal CreatePrincipal(string userId, string role)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role)
        ], "test");

        return new ClaimsPrincipal(identity);
    }

    private static bool IsInExpectedRollout(string userId, int percentage)
    {
        var normalized = Math.Clamp(percentage, 0, 100);
        var bucket = Math.Abs(userId.GetHashCode(StringComparison.Ordinal)) % 100;
        return bucket < normalized;
    }

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T> where T : class, new()
    {
        private readonly T _value;

        public StaticOptionsMonitor(T value)
        {
            _value = value;
        }

        public T CurrentValue => _value;
        public T Get(string? name) => _value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
