using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Infrastructure.Persistence;

public sealed class ConnectionPoolMonitoringInterceptor : DbConnectionInterceptor
{
    private readonly ILogger<ConnectionPoolMonitoringInterceptor> _logger;
    private readonly ConcurrentDictionary<DbConnection, long> _openingTimestamps = new();
    private readonly ConcurrentDictionary<DbConnection, long> _openedTimestamps = new();

    public ConnectionPoolMonitoringInterceptor(ILogger<ConnectionPoolMonitoringInterceptor> logger)
    {
        _logger = logger;
    }

    public override InterceptionResult ConnectionOpening(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result)
    {
        _openingTimestamps[connection] = Stopwatch.GetTimestamp();
        return result;
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (_openingTimestamps.TryRemove(connection, out var openingTimestamp))
        {
            var waitMs = Stopwatch.GetElapsedTime(openingTimestamp).TotalMilliseconds;
            ConnectionPoolMetrics.RecordOpened(waitMs);
        }
        else
        {
            ConnectionPoolMetrics.RecordOpened(0d);
        }

        _openedTimestamps[connection] = Stopwatch.GetTimestamp();
    }

    public override void ConnectionClosed(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (_openedTimestamps.TryRemove(connection, out var openedTimestamp))
        {
            var heldMs = Stopwatch.GetElapsedTime(openedTimestamp).TotalMilliseconds;
            ConnectionPoolMetrics.RecordClosed(heldMs);

            if (heldMs > 30_000d)
            {
                _logger.LogWarning("Potential connection leak detected. Connection held for {HeldMs:F2} ms.", heldMs);
            }
        }
        else
        {
            ConnectionPoolMetrics.RecordClosed(0d);
        }
    }

    public override void ConnectionFailed(DbConnection connection, ConnectionErrorEventData eventData)
    {
        ConnectionPoolMetrics.RecordError();
        _logger.LogWarning(eventData.Exception, "Database connection failed.");
    }
}

public static class ConnectionPoolMetrics
{
    private static long _activeConnections;
    private static long _openedConnections;
    private static long _closedConnections;
    private static long _connectionErrors;
    private static long _waitSamples;
    private static long _waitTicks;
    private static long _heldSamples;
    private static long _heldTicks;

    public static void RecordOpened(double waitMs)
    {
        Interlocked.Increment(ref _activeConnections);
        Interlocked.Increment(ref _openedConnections);
        Interlocked.Increment(ref _waitSamples);
        Interlocked.Add(ref _waitTicks, TimeSpan.FromMilliseconds(waitMs).Ticks);
    }

    public static void RecordClosed(double heldMs)
    {
        Interlocked.Decrement(ref _activeConnections);
        Interlocked.Increment(ref _closedConnections);
        Interlocked.Increment(ref _heldSamples);
        Interlocked.Add(ref _heldTicks, TimeSpan.FromMilliseconds(heldMs).Ticks);
    }

    public static void RecordError()
    {
        Interlocked.Increment(ref _connectionErrors);
    }

    public static ConnectionPoolSnapshot Snapshot(int minPoolSize, int maxPoolSize)
    {
        var opened = Interlocked.Read(ref _openedConnections);
        var closed = Interlocked.Read(ref _closedConnections);
        var idle = Math.Max(0, opened - closed - Interlocked.Read(ref _activeConnections));

        var waitSamples = Interlocked.Read(ref _waitSamples);
        var waitMs = waitSamples == 0
            ? 0d
            : TimeSpan.FromTicks(Interlocked.Read(ref _waitTicks)).TotalMilliseconds / waitSamples;

        var heldSamples = Interlocked.Read(ref _heldSamples);
        var heldMs = heldSamples == 0
            ? 0d
            : TimeSpan.FromTicks(Interlocked.Read(ref _heldTicks)).TotalMilliseconds / heldSamples;

        return new ConnectionPoolSnapshot(
            Interlocked.Read(ref _activeConnections),
            idle,
            waitMs,
            heldMs,
            Interlocked.Read(ref _connectionErrors),
            minPoolSize,
            maxPoolSize);
    }

    public static void Reset()
    {
        Interlocked.Exchange(ref _activeConnections, 0);
        Interlocked.Exchange(ref _openedConnections, 0);
        Interlocked.Exchange(ref _closedConnections, 0);
        Interlocked.Exchange(ref _connectionErrors, 0);
        Interlocked.Exchange(ref _waitSamples, 0);
        Interlocked.Exchange(ref _waitTicks, 0);
        Interlocked.Exchange(ref _heldSamples, 0);
        Interlocked.Exchange(ref _heldTicks, 0);
    }
}

public sealed record ConnectionPoolSnapshot(
    long ActiveConnections,
    long IdleConnections,
    double AvgConnectionWaitMs,
    double AvgConnectionHeldMs,
    long ConnectionErrors,
    int MinimumPoolSize,
    int MaximumPoolSize);
