using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

[Trait("Category", "Valuation")]
[Trait("Category", "Domain")]
public class LandedCostAllocationServiceTests
{
    [Fact]
    public void Allocate_WithSingleRow_ShouldAssignAllCosts()
    {
        var rows = Rows((1, 10m, 10m));

        var allocations = LandedCostAllocationService.Allocate(rows, 100m, 40m, 10m);

        allocations.Should().HaveCount(1);
        allocations[0].FreightCost.Should().Be(100m);
        allocations[0].DutyCost.Should().Be(40m);
        allocations[0].InsuranceCost.Should().Be(10m);
        allocations[0].TotalLandedCost.Should().Be(150m);
    }

    [Fact]
    public void Allocate_WithEqualValueRows_ShouldSplitEvenly()
    {
        var rows = Rows((1, 10m, 10m), (2, 5m, 20m));

        var allocations = LandedCostAllocationService.Allocate(rows, 200m, 0m, 0m);

        allocations[0].FreightCost.Should().Be(100m);
        allocations[1].FreightCost.Should().Be(100m);
    }

    [Fact]
    public void Allocate_WithProportionalValues_ShouldUseItemValueBasis()
    {
        var rows = Rows((1, 100m, 10m), (2, 50m, 20m));

        var allocations = LandedCostAllocationService.Allocate(rows, 200m, 0m, 0m);

        allocations[0].FreightCost.Should().Be(100m);
        allocations[1].FreightCost.Should().Be(100m);
    }

    [Fact]
    public void Allocate_ShouldPreserveFreightTotalAfterRounding()
    {
        var rows = Rows((1, 1m, 1m), (2, 1m, 1m), (3, 1m, 1m));

        var allocations = LandedCostAllocationService.Allocate(rows, 100m, 0m, 0m);

        allocations.Sum(x => x.FreightCost).Should().Be(100m);
        allocations.Select(x => x.FreightCost).Should().ContainInOrder(33.33m, 33.33m, 33.34m);
    }

    [Fact]
    public void Allocate_ShouldPreserveEachComponentTotalAfterRounding()
    {
        var rows = Rows((1, 2m, 10m), (2, 3m, 10m), (3, 5m, 10m));

        var allocations = LandedCostAllocationService.Allocate(rows, 123.45m, 88.12m, 9.87m);

        allocations.Sum(x => x.FreightCost).Should().Be(123.45m);
        allocations.Sum(x => x.DutyCost).Should().Be(88.12m);
        allocations.Sum(x => x.InsuranceCost).Should().Be(9.87m);
    }

    [Fact]
    public void Allocate_WithZeroCosts_ShouldReturnZeroAllocations()
    {
        var rows = Rows((1, 10m, 10m), (2, 20m, 10m));

        var allocations = LandedCostAllocationService.Allocate(rows, 0m, 0m, 0m);

        allocations.All(x => x.TotalLandedCost == 0m).Should().BeTrue();
    }

    [Fact]
    public void Allocate_WhenValueBasisIsZero_ShouldFallbackToQuantity()
    {
        var rows = Rows((1, 10m, 0m), (2, 20m, 0m));

        var allocations = LandedCostAllocationService.Allocate(rows, 30m, 0m, 0m);

        allocations[0].FreightCost.Should().Be(10m);
        allocations[1].FreightCost.Should().Be(20m);
    }

    [Fact]
    public void Allocate_WhenValueAndQuantityAreZero_ShouldFallbackToEqualWeights()
    {
        var rows = Rows((1, 0m, 0m), (2, 0m, 0m), (3, 0m, 0m));

        var allocations = LandedCostAllocationService.Allocate(rows, 9m, 0m, 0m);

        allocations.Select(x => x.FreightCost).Should().ContainInOrder(3m, 3m, 3m);
    }

    [Fact]
    public void Allocate_WithNegativeFreight_ShouldThrow()
    {
        var rows = Rows((1, 1m, 1m));

        var action = () => LandedCostAllocationService.Allocate(rows, -1m, 0m, 0m);

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void Allocate_WithNegativeDuty_ShouldThrow()
    {
        var rows = Rows((1, 1m, 1m));

        var action = () => LandedCostAllocationService.Allocate(rows, 0m, -1m, 0m);

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void Allocate_WithNegativeInsurance_ShouldThrow()
    {
        var rows = Rows((1, 1m, 1m));

        var action = () => LandedCostAllocationService.Allocate(rows, 0m, 0m, -1m);

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void Allocate_WithNoRows_ShouldThrow()
    {
        var action = () => LandedCostAllocationService.Allocate(Array.Empty<OnHandValue>(), 1m, 1m, 1m);

        action.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void Allocate_ShouldKeepOrderOfInputRows()
    {
        var rows = Rows((5, 10m, 10m), (3, 20m, 10m), (9, 30m, 10m));

        var allocations = LandedCostAllocationService.Allocate(rows, 60m, 0m, 0m);

        allocations.Select(x => x.ItemId).Should().ContainInOrder(5, 3, 9);
    }

    [Fact]
    public void Allocate_TotalLandedCost_ShouldEqualComponentSumPerRow()
    {
        var rows = Rows((1, 1m, 5m), (2, 2m, 5m));

        var allocations = LandedCostAllocationService.Allocate(rows, 9m, 6m, 3m);

        allocations.All(x => x.TotalLandedCost == decimal.Round(x.FreightCost + x.DutyCost + x.InsuranceCost, 2, MidpointRounding.AwayFromZero))
            .Should()
            .BeTrue();
    }

    [Fact]
    public void Allocate_Components_ShouldUseTwoDecimalPrecision()
    {
        var rows = Rows((1, 1m, 1m), (2, 1m, 1m), (3, 1m, 1m));

        var allocations = LandedCostAllocationService.Allocate(rows, 10m, 10m, 10m);

        allocations.All(x => decimal.Round(x.FreightCost, 2) == x.FreightCost).Should().BeTrue();
        allocations.All(x => decimal.Round(x.DutyCost, 2) == x.DutyCost).Should().BeTrue();
        allocations.All(x => decimal.Round(x.InsuranceCost, 2) == x.InsuranceCost).Should().BeTrue();
    }

    private static IReadOnlyList<OnHandValue> Rows(params (int itemId, decimal qty, decimal unitCost)[] source)
    {
        return source
            .Select(x => new OnHandValue
            {
                Id = Guid.NewGuid(),
                ItemId = x.itemId,
                Qty = x.qty,
                UnitCost = x.unitCost
            })
            .ToList();
    }
}
