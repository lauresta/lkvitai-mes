using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Domain.Aggregates;
using LKvitai.MES.SharedKernel;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

[Trait("Category", "Valuation")]
[Trait("Category", "Domain")]
public class ItemValuationTests
{
    [Fact]
    public void StreamIdFor_ShouldUseSprint7Format()
    {
        var streamId = ItemValuation.StreamIdFor(42);

        streamId.Should().Be("valuation-item-42");
    }

    [Fact]
    public void StreamIdFor_WithInvalidItemId_ShouldThrow()
    {
        var action = () => ItemValuation.StreamIdFor(0);

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void Initialize_ShouldCreateValuationInitializedEvent()
    {
        var aggregate = new ItemValuation();
        var commandId = Guid.NewGuid();

        var evt = aggregate.Initialize(10, 12.34567m, "Initial purchase", "accountant", commandId);

        evt.InventoryItemId.Should().Be(10);
        evt.InitialUnitCost.Should().Be(12.3457m);
        evt.Reason.Should().Be("Initial purchase");
        evt.CommandId.Should().Be(commandId);
    }

    [Fact]
    public void Initialize_WithNegativeCost_ShouldThrow()
    {
        var aggregate = new ItemValuation();

        var action = () => aggregate.Initialize(10, -1m, "Initial purchase", "accountant", Guid.NewGuid());

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void Initialize_WhenAlreadyInitialized_ShouldThrow()
    {
        var aggregate = CreateInitializedAggregate();

        var action = () => aggregate.Initialize(10, 20m, "Duplicate", "accountant", Guid.NewGuid());

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void Apply_ValuationInitialized_ShouldSetState()
    {
        var aggregate = new ItemValuation();
        var evt = CreateInitializedEvent();

        aggregate.Apply(evt);

        aggregate.ItemId.Should().Be(10);
        aggregate.CurrentCost.Should().Be(10m);
        aggregate.IsInitialized.Should().BeTrue();
        aggregate.Version.Should().Be(1);
    }

    [Fact]
    public void AdjustCost_ShouldCreateCostAdjustedEvent()
    {
        var aggregate = CreateInitializedAggregate();

        var evt = aggregate.AdjustCost(14.2m, "Supplier increase", "finance", "manager@example.com", Guid.NewGuid());

        evt.OldUnitCost.Should().Be(10m);
        evt.NewUnitCost.Should().Be(14.2m);
        evt.InventoryItemId.Should().Be(10);
        evt.ApprovedBy.Should().Be("manager@example.com");
    }

    [Fact]
    public void AdjustCost_WithNegativeCost_ShouldThrow()
    {
        var aggregate = CreateInitializedAggregate();

        var action = () => aggregate.AdjustCost(-0.1m, "Bad", "finance", null, Guid.NewGuid());

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void AdjustCost_WithoutReason_ShouldThrow()
    {
        var aggregate = CreateInitializedAggregate();

        var action = () => aggregate.AdjustCost(11m, " ", "finance", null, Guid.NewGuid());

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void Apply_CostAdjusted_ShouldUpdateCostAndVersion()
    {
        var aggregate = CreateInitializedAggregate();

        aggregate.Apply(new CostAdjusted
        {
            InventoryItemId = 10,
            ItemId = Valuation.ToValuationItemId(10),
            OldUnitCost = 10m,
            NewUnitCost = 13.55555m,
            Reason = "Market",
            AdjustedBy = "finance",
            CommandId = Guid.NewGuid()
        });

        aggregate.CurrentCost.Should().Be(13.5556m);
        aggregate.Version.Should().Be(2);
    }

    [Fact]
    public void ApplyLandedCost_ShouldCreateEventWithTotal()
    {
        var aggregate = CreateInitializedAggregate();
        var shipmentId = Guid.NewGuid();

        var evt = aggregate.ApplyLandedCost(100m, 25m, 10m, shipmentId, "ops", Guid.NewGuid());

        evt.ItemId.Should().Be(10);
        evt.TotalLandedCost.Should().Be(135m);
        evt.ShipmentId.Should().Be(shipmentId);
    }

    [Fact]
    public void ApplyLandedCost_WithNegativePart_ShouldThrow()
    {
        var aggregate = CreateInitializedAggregate();

        var action = () => aggregate.ApplyLandedCost(-1m, 0m, 0m, Guid.NewGuid(), "ops", Guid.NewGuid());

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void Apply_LandedCostApplied_ShouldIncreaseCost()
    {
        var aggregate = CreateInitializedAggregate();

        aggregate.Apply(new LandedCostApplied
        {
            ItemId = 10,
            FreightCost = 1m,
            DutyCost = 0.5m,
            InsuranceCost = 0.5m,
            TotalLandedCost = 2m,
            ShipmentId = Guid.NewGuid(),
            AppliedBy = "ops",
            CommandId = Guid.NewGuid()
        });

        aggregate.CurrentCost.Should().Be(12m);
        aggregate.Version.Should().Be(2);
    }

    [Fact]
    public void WriteDown_ShouldCreateWrittenDownEvent()
    {
        var aggregate = CreateInitializedAggregate();

        var evt = aggregate.WriteDown(8m, "Damage", "manager@example.com", Guid.NewGuid());

        evt.OldValue.Should().Be(10m);
        evt.NewValue.Should().Be(8m);
        evt.ApprovedBy.Should().Be("manager@example.com");
    }

    [Fact]
    public void WriteDown_LargeDeltaWithoutApproval_ShouldThrow()
    {
        var aggregate = CreateInitializedAggregate(initialCost: 5000m);

        var action = () => aggregate.WriteDown(2000m, "Damage", null, Guid.NewGuid());

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void WriteDown_IncreasingValue_ShouldThrow()
    {
        var aggregate = CreateInitializedAggregate();

        var action = () => aggregate.WriteDown(15m, "Invalid", "manager", Guid.NewGuid());

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void Apply_WrittenDown_ShouldSetNewCost()
    {
        var aggregate = CreateInitializedAggregate();

        aggregate.Apply(new WrittenDown
        {
            ItemId = 10,
            OldValue = 10m,
            NewValue = 7.5m,
            Reason = "Obsolete",
            ApprovedBy = "manager",
            CommandId = Guid.NewGuid()
        });

        aggregate.CurrentCost.Should().Be(7.5m);
        aggregate.Version.Should().Be(2);
    }

    private static ItemValuation CreateInitializedAggregate(decimal initialCost = 10m)
    {
        var aggregate = new ItemValuation();
        aggregate.Apply(new ValuationInitialized
        {
            InventoryItemId = 10,
            ItemId = Valuation.ToValuationItemId(10),
            InitialUnitCost = initialCost,
            Reason = "Initial purchase",
            Source = "MANUAL",
            InitializedBy = "seed",
            InitializedAt = DateTime.UtcNow,
            CommandId = Guid.NewGuid()
        });

        return aggregate;
    }

    private static ValuationInitialized CreateInitializedEvent()
    {
        return new ValuationInitialized
        {
            InventoryItemId = 10,
            ItemId = Valuation.ToValuationItemId(10),
            InitialUnitCost = 10m,
            Reason = "Initial purchase",
            Source = "MANUAL",
            InitializedBy = "seed",
            InitializedAt = DateTime.UtcNow,
            CommandId = Guid.NewGuid()
        };
    }
}
