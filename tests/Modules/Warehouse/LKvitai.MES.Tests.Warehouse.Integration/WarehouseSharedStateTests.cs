using DotNet.Testcontainers.Builders;
using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using StackExchange.Redis;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Integration;

public sealed class WarehouseSharedStateTests
{
    [SkippableFact]
    public async Task LotTraceReport_AndComplianceIndex_AreSharedAcrossInstancesAndReconnects()
    {
        DockerRequirement.EnsureEnabled();
        await using var container = new ContainerBuilder().WithImage("redis:7-alpine")
            .WithPortBinding(6379, true).WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(6379)).Build();
        await container.StartAsync();
        var connection = $"{container.Hostname}:{container.GetMappedPublicPort(6379)}";
        using var first = await ConnectionMultiplexer.ConnectAsync(connection);
        using var second = await ConnectionMultiplexer.ConnectAsync(connection);
        var writer = new RedisLotTraceStore(first);
        var reader = new RedisLotTraceStore(second);
        var report = new LotTraceReport(Guid.NewGuid(), "QA-C7-lot", LotTraceDirection.Forward,
            new LotTraceNode("LOT", "QA-C7", "QA-C7", DateTimeOffset.UtcNow, []), false, DateTimeOffset.UtcNow);
        writer.Save(report);
        reader.TryGet(report.TraceId, out var read).Should().BeTrue();
        read.Should().BeEquivalentTo(report);
        writer.Save(report); // retry must not duplicate the shared index
        reader.Count.Should().Be(1);
        reader.TryGetAny(out var any).Should().BeTrue();
        any!.TraceId.Should().Be(report.TraceId);
        first.Dispose();
        using var restarted = await ConnectionMultiplexer.ConnectAsync(connection);
        var afterRestart = new RedisLotTraceStore(restarted);
        afterRestart.Count.Should().Be(1);
        afterRestart.TryGetAny(out var persisted).Should().BeTrue();
        persisted!.LotNumber.Should().Be("QA-C7-lot");
        afterRestart.TryGet(Guid.NewGuid(), out _).Should().BeFalse();
    }
}
