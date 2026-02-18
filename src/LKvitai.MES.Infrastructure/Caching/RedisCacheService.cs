using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace LKvitai.MES.Infrastructure.Caching;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
    CacheMetricsSnapshot GetMetrics();
}

public sealed class RedisCacheService : ICacheService, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ILogger<RedisCacheService> _logger;
    private readonly ConcurrentDictionary<string, byte> _trackedKeys = new(StringComparer.Ordinal);
    private readonly IConnectionMultiplexer? _connection;
    private readonly IDatabase? _db;

    private long _hits;
    private long _misses;
    private long _reads;
    private long _writes;
    private long _removes;
    private long _latencyTicks;

    public RedisCacheService(ILogger<RedisCacheService> logger, string? redisConnectionString)
    {
        _logger = logger;

        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            _logger.LogWarning("Redis connection string is empty. Cache will be bypassed.");
            return;
        }

        try
        {
            var options = ConfigurationOptions.Parse(redisConnectionString);
            options.AbortOnConnectFail = false;
            options.ConnectRetry = Math.Max(3, options.ConnectRetry);
            options.ConnectTimeout = 1000;
            options.SyncTimeout = 1000;
            _connection = ConnectionMultiplexer.Connect(options);
            _db = _connection.GetDatabase();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable, cache bypassed.");
        }
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        Interlocked.Increment(ref _reads);

        if (_db is null)
        {
            Interlocked.Increment(ref _misses);
            TrackLatency(started);
            return default;
        }

        try
        {
            var value = await _db.StringGetAsync(key);
            if (!value.HasValue)
            {
                Interlocked.Increment(ref _misses);
                TrackLatency(started);
                return default;
            }

            Interlocked.Increment(ref _hits);
            TrackLatency(started);
            var json = value.ToString();
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis get failed for key {Key}. Cache bypassed.", key);
            Interlocked.Increment(ref _misses);
            TrackLatency(started);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _writes);
        if (_db is null)
        {
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            await _db.StringSetAsync(key, json, ttl);
            _trackedKeys[key] = 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis set failed for key {Key}.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _removes);
        _trackedKeys.TryRemove(key, out _);

        if (_db is null)
        {
            return;
        }

        try
        {
            await _db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis remove failed for key {Key}.", key);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var keys = _trackedKeys.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        foreach (var key in keys)
        {
            await RemoveAsync(key, cancellationToken);
        }
    }

    public CacheMetricsSnapshot GetMetrics()
    {
        var reads = Interlocked.Read(ref _reads);
        var hits = Interlocked.Read(ref _hits);
        var misses = Interlocked.Read(ref _misses);
        var hitRate = reads == 0 ? 0d : (double)hits / reads;
        var latencyMs = reads == 0 ? 0d : TimeSpan.FromTicks(Interlocked.Read(ref _latencyTicks)).TotalMilliseconds / reads;

        return new CacheMetricsSnapshot(
            reads,
            hits,
            misses,
            hitRate,
            latencyMs,
            Interlocked.Read(ref _writes),
            Interlocked.Read(ref _removes),
            _trackedKeys.Count);
    }

    private void TrackLatency(long startedTimestamp)
    {
        var elapsedSeconds = (Stopwatch.GetTimestamp() - startedTimestamp) / (double)Stopwatch.Frequency;
        Interlocked.Add(ref _latencyTicks, TimeSpan.FromSeconds(elapsedSeconds).Ticks);
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}

public sealed record CacheMetricsSnapshot(
    long Reads,
    long Hits,
    long Misses,
    double HitRate,
    double AverageLatencyMs,
    long Writes,
    long Removes,
    int TrackedKeyCount);

public sealed class NoOpCacheService : ICacheService
{
    private static readonly CacheMetricsSnapshot Empty = new(0, 0, 0, 0d, 0d, 0, 0, 0);

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => Task.FromResult<T?>(default);
    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public CacheMetricsSnapshot GetMetrics() => Empty;
}
