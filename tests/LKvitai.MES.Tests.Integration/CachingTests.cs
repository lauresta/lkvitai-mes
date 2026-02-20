using LKvitai.MES.Modules.Warehouse.Infrastructure.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public sealed class CachingTests
{
    [Fact]
    public async Task GetAsync_WhenRedisUnavailable_ReturnsDefault()
    {
        var sut = CreateService();

        var value = await sut.GetAsync<string>("item:1");

        Assert.Null(value);
    }

    [Fact]
    public async Task SetAsync_WhenRedisUnavailable_DoesNotThrow()
    {
        var sut = CreateService();

        await sut.SetAsync("item:1", new { Name = "Sample" }, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task RemoveAsync_WhenRedisUnavailable_DoesNotThrow()
    {
        var sut = CreateService();

        await sut.RemoveAsync("item:1");
    }

    [Fact]
    public async Task RemoveByPrefixAsync_WhenRedisUnavailable_DoesNotThrow()
    {
        var sut = CreateService();

        await sut.RemoveByPrefixAsync("item:");
    }

    [Fact]
    public async Task Metrics_ShouldTrackReadsAndMisses()
    {
        var sut = CreateService();

        _ = await sut.GetAsync<string>("item:1");
        _ = await sut.GetAsync<string>("item:2");

        var metrics = sut.GetMetrics();
        Assert.Equal(2, metrics.Reads);
        Assert.Equal(2, metrics.Misses);
        Assert.Equal(0, metrics.Hits);
    }

    [Fact]
    public async Task MetricsEndpointShape_ShouldRemainPrometheusFriendly()
    {
        var sut = CreateService();

        await sut.SetAsync("item:1", "value", TimeSpan.FromSeconds(1));
        await sut.RemoveAsync("item:1");

        var metrics = sut.GetMetrics();
        Assert.True(metrics.Writes >= 1);
        Assert.True(metrics.Removes >= 1);
    }

    private static RedisCacheService CreateService()
    {
        return new RedisCacheService(new NullLogger<RedisCacheService>(), "127.0.0.1:6399,abortConnect=false,connectTimeout=200,syncTimeout=200");
    }
}
