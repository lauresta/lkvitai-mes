using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public class ValuationLifecycleIntegrationTests
{
    [Fact]
    public void ItemValuation_Lifecycle_ShouldApplyAllSprint7EventsInOrder()
    {
        var aggregate = new ItemValuation();

        var initialized = aggregate.Initialize(
            itemId: 120,
            initialCost: 10m,
            reason: "Initial purchase",
            initializedBy: "accountant@example.com",
            commandId: Guid.NewGuid());
        aggregate.Apply(initialized);

        var adjusted = aggregate.AdjustCost(
            newCost: 12m,
            reason: "Supplier increase",
            adjustedBy: "accountant@example.com",
            approvedBy: "manager@example.com",
            commandId: Guid.NewGuid());
        aggregate.Apply(adjusted);

        var landed = aggregate.ApplyLandedCost(
            freightCost: 0.25m,
            dutyCost: 0.1m,
            insuranceCost: 0.05m,
            shipmentId: Guid.NewGuid(),
            appliedBy: "accountant@example.com",
            commandId: Guid.NewGuid());
        aggregate.Apply(landed);

        var writeDown = aggregate.WriteDown(
            newValue: 11m,
            reason: "Damage adjustment",
            approvedBy: "manager@example.com",
            commandId: Guid.NewGuid());
        aggregate.Apply(writeDown);

        aggregate.ItemId.Should().Be(120);
        aggregate.IsInitialized.Should().BeTrue();
        aggregate.CurrentCost.Should().Be(11m);
        aggregate.Version.Should().Be(4);
    }
}
