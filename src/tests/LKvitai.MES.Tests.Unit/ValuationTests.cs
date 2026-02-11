using System.Text.Json;
using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Domain.Aggregates;
using LKvitai.MES.SharedKernel;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

[Trait("Category", "Valuation")]
[Trait("Category", "Domain")]
public class ValuationTests
{
    [Fact]
    public void StreamIdFor_ShouldUseLowercaseNamingConvention()
    {
        var itemId = Guid.Parse("A1B2C3D4-E5F6-7890-ABCD-EF1234567890");

        var streamId = Valuation.StreamIdFor(itemId);

        streamId.Should().Be("valuation-a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    }

    [Fact]
    public void ToValuationItemId_ShouldRoundTripInventoryItemId()
    {
        const int inventoryItemId = 101;

        var valuationItemId = Valuation.ToValuationItemId(inventoryItemId);
        var success = Valuation.TryToInventoryItemId(valuationItemId, out var restoredInventoryItemId);

        success.Should().BeTrue();
        restoredInventoryItemId.Should().Be(inventoryItemId);
    }

    [Fact]
    public void Initialize_ShouldCreateValuationInitializedEvent()
    {
        var valuation = new Valuation();
        var commandId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var evt = valuation.Initialize(itemId, 10.56789m, "GoodsReceipt", "accountant", commandId);

        evt.ItemId.Should().Be(itemId);
        evt.InitialUnitCost.Should().Be(10.5679m);
        evt.CommandId.Should().Be(commandId);
        evt.SchemaVersion.Should().Be("v1");
    }

    [Fact]
    public void ApplyValuationInitialized_ShouldSetAggregateState()
    {
        var valuation = new Valuation();
        var itemId = Guid.NewGuid();

        valuation.Apply(new ValuationInitialized
        {
            ItemId = itemId,
            InitialUnitCost = 12.5m,
            Source = "Manual",
            InitializedBy = "user",
            InitializedAt = DateTime.UtcNow,
            CommandId = Guid.NewGuid()
        });

        valuation.ItemId.Should().Be(itemId);
        valuation.UnitCost.Should().Be(12.5m);
        valuation.Version.Should().Be(1);
        valuation.Id.Should().Be(Valuation.StreamIdFor(itemId));
    }

    [Fact]
    public void AdjustCost_WhenNotInitialized_ShouldThrow()
    {
        var valuation = new Valuation();

        var action = () => valuation.AdjustCost(20m, "Market", "finance", Guid.NewGuid());

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void AdjustCost_ShouldCreateEventWithOldAndNewCost()
    {
        var valuation = CreateInitializedValuation(15m);

        var evt = valuation.AdjustCost(18m, "Vendor price increase", "finance", Guid.NewGuid());

        evt.OldUnitCost.Should().Be(15m);
        evt.NewUnitCost.Should().Be(18m);
        evt.Reason.Should().Be("Vendor price increase");
        evt.SchemaVersion.Should().Be("v1");
    }

    [Fact]
    public void ApplyCostAdjusted_ShouldUpdateStateAndIncrementVersion()
    {
        var valuation = CreateInitializedValuation(15m);

        valuation.Apply(new CostAdjusted
        {
            ItemId = valuation.ItemId,
            OldUnitCost = 15m,
            NewUnitCost = 17.12345m,
            Reason = "Market update",
            AdjustedBy = "finance",
            AdjustedAt = DateTime.UtcNow,
            CommandId = Guid.NewGuid()
        });

        valuation.UnitCost.Should().Be(17.1235m);
        valuation.Version.Should().Be(2);
    }

    [Fact]
    public void AllocateLandedCost_ShouldIncreaseUnitCost()
    {
        var valuation = CreateInitializedValuation(10m);

        var evt = valuation.AllocateLandedCost(
            1.23456m,
            Guid.NewGuid(),
            "EVEN_SPLIT",
            "ops.user",
            Guid.NewGuid());

        evt.OldUnitCost.Should().Be(10m);
        evt.LandedCostPerUnit.Should().Be(1.2346m);
        evt.NewUnitCost.Should().Be(11.2346m);
    }

    [Fact]
    public void WriteDown_ShouldCalculateFinancialImpact()
    {
        var valuation = CreateInitializedValuation(50m);

        var evt = valuation.WriteDown(0.2m, "Damaged", "manager", 100m, Guid.NewGuid());

        evt.NewUnitCost.Should().Be(40m);
        evt.FinancialImpact.Should().Be(1000m);
        evt.SchemaVersion.Should().Be("v1");
    }

    [Fact]
    public void WriteDown_InvalidPercentage_ShouldThrow()
    {
        var valuation = CreateInitializedValuation(50m);

        var action = () => valuation.WriteDown(1.2m, "Damaged", "manager", 10m, Guid.NewGuid());

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    private static Valuation CreateInitializedValuation(decimal unitCost)
    {
        var valuation = new Valuation();
        valuation.Apply(new ValuationInitialized
        {
            ItemId = Guid.NewGuid(),
            InitialUnitCost = unitCost,
            Source = "Manual",
            InitializedBy = "seed",
            InitializedAt = DateTime.UtcNow,
            CommandId = Guid.NewGuid()
        });

        return valuation;
    }
}

[Trait("Category", "Valuation")]
[Trait("Category", "Domain")]
public class ValuationConcurrencyTests
{
    [Fact]
    public void EnsureExpectedVersion_WhenVersionMatches_ShouldNotThrow()
    {
        var valuation = new Valuation();
        valuation.Apply(new ValuationInitialized
        {
            ItemId = Guid.NewGuid(),
            InitialUnitCost = 10m,
            Source = "Manual",
            InitializedBy = "seed",
            InitializedAt = DateTime.UtcNow,
            CommandId = Guid.NewGuid()
        });

        var action = () => valuation.EnsureExpectedVersion(1);

        action.Should().NotThrow();
    }

    [Fact]
    public void EnsureExpectedVersion_WhenVersionDiffers_ShouldThrowConcurrencyConflict()
    {
        var valuation = new Valuation();
        valuation.Apply(new ValuationInitialized
        {
            ItemId = Guid.NewGuid(),
            InitialUnitCost = 10m,
            Source = "Manual",
            InitializedBy = "seed",
            InitializedAt = DateTime.UtcNow,
            CommandId = Guid.NewGuid()
        });

        var action = () => valuation.EnsureExpectedVersion(0);

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be(DomainErrorCodes.ConcurrencyConflict);
    }
}

[Trait("Category", "Valuation")]
[Trait("Category", "Domain")]
public class ValuationSerializationTests
{
    [Fact]
    public void ValuationEventSerialization_ShouldIncludeSchemaVersion()
    {
        var evt = new LandedCostAllocated
        {
            ItemId = Guid.NewGuid(),
            OldUnitCost = 12m,
            LandedCostPerUnit = 1.2m,
            NewUnitCost = 13.2m,
            InboundShipmentId = Guid.NewGuid(),
            AllocationMethod = "EVEN_SPLIT",
            AllocatedBy = "user",
            AllocatedAt = DateTime.UtcNow,
            CommandId = Guid.NewGuid()
        };

        var json = JsonSerializer.Serialize(evt);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        root.TryGetProperty("SchemaVersion", out var schemaVersion).Should().BeTrue();
        schemaVersion.GetString().Should().Be("v1");
    }

    [Fact]
    public void ValuationEventSerialization_ShouldRoundtrip()
    {
        var evt = new StockWrittenDown
        {
            ItemId = Guid.NewGuid(),
            OldUnitCost = 22m,
            WriteDownPercentage = 0.15m,
            NewUnitCost = 18.7m,
            Reason = "Obsolete",
            ApprovedBy = "cfo",
            ApprovedAt = DateTime.UtcNow,
            QuantityAffected = 40m,
            FinancialImpact = 132m,
            CommandId = Guid.NewGuid()
        };

        var json = JsonSerializer.Serialize(evt);
        var restored = JsonSerializer.Deserialize<StockWrittenDown>(json);

        restored.Should().NotBeNull();
        restored!.ItemId.Should().Be(evt.ItemId);
        restored.CommandId.Should().Be(evt.CommandId);
        restored.SchemaVersion.Should().Be("v1");
    }
}
