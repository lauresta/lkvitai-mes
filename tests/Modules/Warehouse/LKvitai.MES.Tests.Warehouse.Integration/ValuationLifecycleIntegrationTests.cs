using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Integration;

public class ValuationLifecycleIntegrationTests
{
    [Fact]
    public void ItemValuation_Lifecycle_ShouldApplyValueEventsInOrder()
    {
        var doc = new StockValueDocument("Test", "1", "T-1", "lifecycle");
        var aggregate = new ItemValuation();

        aggregate.Apply(aggregate.Inflow(120, StockValueKinds.Receipt, 10m, 100m, doc));
        aggregate.Apply(aggregate.Inflow(120, StockValueKinds.Receipt, 10m, 120m, doc));
        aggregate.ValueEur.Should().Be(220m);
        aggregate.UnitCost.Should().Be(11m);

        aggregate.Apply(aggregate.Outflow(120, StockValueKinds.Issue, 5m, doc)!);
        aggregate.ValueEur.Should().Be(165m);

        aggregate.Apply(aggregate.Revalue(120, 5m, doc));
        aggregate.ValueEur.Should().Be(75m);
        aggregate.UnitCost.Should().Be(5m);
        aggregate.Version.Should().Be(4);
        aggregate.ItemId.Should().Be(120);
    }
}
