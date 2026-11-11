using FluentAssertions;
using LKvitai.MES.Contracts.Messages;
using LKvitai.MES.Modules.Warehouse.Sagas;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class AgnumExportSagaHarnessTests
{
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
