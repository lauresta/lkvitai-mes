using FluentAssertions;
using LKvitai.MES.Contracts.Messages;
using LKvitai.MES.Modules.Warehouse.Sagas;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Hangfire;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Moq;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class AgnumExportSagaHarnessTests
{
    [Fact]
    public async Task RealCsvJob_UsesPersistedHistoryIdForTheEntireSaga()
    {
        var root = Path.Combine(Path.GetTempPath(), $"QA-C8-export-{Guid.NewGuid():N}");
        await using var db = new WarehouseDbContext(new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"QA-C8-export-{Guid.NewGuid():N}").Options);
        db.AgnumExportConfigs.Add(new AgnumExportConfig
        {
            Id = Guid.NewGuid(), Scope = AgnumExportScope.TotalOnly, Format = AgnumExportFormat.Csv,
            Schedule = "0 0 1 1 *", IsActive = true, UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x => x.AddSagaStateMachine<AgnumExportSaga, AgnumExportSagaState>())
            .BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        harness.TestTimeout = TimeSpan.FromSeconds(10);
        await harness.Start();
        try
        {
            var bus = new MassTransitEventBus(harness.Bus);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["Agnum:ExportRootPath"] = root }).Build();
            var orchestrator = new AgnumExportOrchestrator(db, bus,
                new Mock<IHttpClientFactory>(MockBehavior.Strict).Object,
                new Mock<IAgnumSecretProtector>(MockBehavior.Strict).Object,
                configuration, NullLoggerFactory.Instance.CreateLogger<AgnumExportOrchestrator>());
            var job = new AgnumExportRecurringJob(orchestrator,
                new Mock<IBackgroundJobClient>(MockBehavior.Strict).Object, bus,
                NullLoggerFactory.Instance.CreateLogger<AgnumExportRecurringJob>());

            var result = await job.ExecuteAsync("QA-C8-export");
            result.IsSuccess.Should().BeTrue();
            var history = await db.AgnumExportHistories.SingleAsync();
            result.HistoryId.Should().Be(history.Id);
            var saga = harness.GetSagaStateMachineHarness<AgnumExportSaga, AgnumExportSagaState>();
            (await saga.Consumed.Any<StartAgnumExport>()).Should().BeTrue();
            var started = harness.Published.Select<StartAgnumExport>().Single().Context.Message;
            started.CorrelationId.Should().Be(history.Id);
            (await saga.Consumed.Any<AgnumExportSucceeded>()).Should().BeTrue();
            harness.Published.Select<AgnumExportSucceeded>().Single().Context.Message.CorrelationId.Should().Be(history.Id);
            (await saga.NotExists(history.Id)).Should().BeNull();
            (await harness.Published.Any<Fault<StartAgnumExport>>()).Should().BeFalse();
        }
        finally
        {
            await harness.Stop();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Export_StartsProcessing_ThenFinalizesWithoutFaults()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x => x.AddSagaStateMachine<AgnumExportSaga, AgnumExportSagaState>())
            .BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        harness.TestTimeout = TimeSpan.FromSeconds(10);
        await harness.Start();
        try
        {
            var id = Guid.NewGuid();
            var saga = harness.GetSagaStateMachineHarness<AgnumExportSaga, AgnumExportSagaState>();
            await harness.Bus.Publish(new StartAgnumExport { CorrelationId = id, Trigger = "QA-C8" });
            (await saga.Exists(id, x => x.Processing)).Should().Be(id);
            await harness.Bus.Publish(new AgnumExportSucceeded { CorrelationId = id, ExportNumber = "QA-C8", RowCount = 2 });
            (await saga.Consumed.Any<AgnumExportSucceeded>()).Should().BeTrue();
            (await saga.NotExists(id)).Should().BeNull();
            (await harness.Published.Any<Fault<StartAgnumExport>>()).Should().BeFalse();
        }
        finally { await harness.Stop(); }
    }
}
