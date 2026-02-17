using System.Text.Json;
using LKvitai.MES.Infrastructure.Persistence;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public sealed class ConnectionPoolingTests
{
    [Fact]
    public void DevelopmentConnectionString_ShouldContainPoolingParameters()
    {
        var configPath = ResolveFromRepositoryRoot("src/LKvitai.MES.Api/appsettings.Development.json");
        var json = File.ReadAllText(configPath);
        using var doc = JsonDocument.Parse(json);
        var connectionString = doc.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("WarehouseDb")
            .GetString();

        Assert.NotNull(connectionString);
        Assert.Contains("Minimum Pool Size=10", connectionString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Maximum Pool Size=100", connectionString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Connection Lifetime=300", connectionString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Connection Idle Lifetime=60", connectionString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Timeout=30", connectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Snapshot_ShouldTrackActiveAndIdleConnections()
    {
        ConnectionPoolMetrics.Reset();
        ConnectionPoolMetrics.RecordOpened(2.5d);
        ConnectionPoolMetrics.RecordOpened(5d);
        ConnectionPoolMetrics.RecordClosed(10d);

        var snapshot = ConnectionPoolMetrics.Snapshot(10, 100);

        Assert.Equal(1, snapshot.ActiveConnections);
        Assert.Equal(0, snapshot.IdleConnections);
    }

    [Fact]
    public void Snapshot_ShouldTrackWaitAndHeldDurations()
    {
        ConnectionPoolMetrics.Reset();
        ConnectionPoolMetrics.RecordOpened(5d);
        ConnectionPoolMetrics.RecordOpened(15d);
        ConnectionPoolMetrics.RecordClosed(100d);
        ConnectionPoolMetrics.RecordClosed(300d);

        var snapshot = ConnectionPoolMetrics.Snapshot(10, 100);

        Assert.True(snapshot.AvgConnectionWaitMs >= 10d);
        Assert.True(snapshot.AvgConnectionHeldMs >= 200d);
    }

    [Fact]
    public void Snapshot_ShouldTrackConnectionErrors()
    {
        ConnectionPoolMetrics.Reset();
        ConnectionPoolMetrics.RecordError();
        ConnectionPoolMetrics.RecordError();

        var snapshot = ConnectionPoolMetrics.Snapshot(10, 100);

        Assert.Equal(2, snapshot.ConnectionErrors);
    }

    [Fact]
    public void Snapshot_ShouldExposeConfiguredPoolBounds()
    {
        ConnectionPoolMetrics.Reset();

        var snapshot = ConnectionPoolMetrics.Snapshot(10, 100);

        Assert.Equal(10, snapshot.MinimumPoolSize);
        Assert.Equal(100, snapshot.MaximumPoolSize);
    }

    private static string ResolveFromRepositoryRoot(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not resolve file from repository root: {relativePath}");
    }
}
