using FluentAssertions;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.SharedKernel;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

[Trait("Category", "Valuation")]
[Trait("Category", "Domain")]
public class ValuationCostAdjustmentPolicyTests
{
    [Fact]
    public void ValidateRequest_WithValidData_ShouldPass()
    {
        var result = ValuationCostAdjustmentPolicy.ValidateRequest(CreateCommand());

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateRequest_WithInvalidItemId_ShouldFail()
    {
        var result = ValuationCostAdjustmentPolicy.ValidateRequest(CreateCommand(itemId: 0));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void ValidateRequest_WithNegativeCost_ShouldFail()
    {
        var result = ValuationCostAdjustmentPolicy.ValidateRequest(CreateCommand(newCost: -0.01m));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void ValidateRequest_WithShortReason_ShouldFail()
    {
        var result = ValuationCostAdjustmentPolicy.ValidateRequest(CreateCommand(reason: "short"));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void ValidateRequest_WithWhitespaceReason_ShouldFail()
    {
        var result = ValuationCostAdjustmentPolicy.ValidateRequest(CreateCommand(reason: "   "));

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void CalculateDeltaPercent_WithZeroOldAndZeroNew_ShouldBeZero()
    {
        var percent = ValuationCostAdjustmentPolicy.CalculateDeltaPercent(0m, 0m);

        percent.Should().Be(0m);
    }

    [Fact]
    public void CalculateDeltaPercent_WithZeroOldAndPositiveNew_ShouldBeHundred()
    {
        var percent = ValuationCostAdjustmentPolicy.CalculateDeltaPercent(0m, 10m);

        percent.Should().Be(100m);
    }

    [Fact]
    public void CalculateDeltaPercent_WithIncrease_ShouldMatchExpected()
    {
        var percent = ValuationCostAdjustmentPolicy.CalculateDeltaPercent(10m, 12m);

        percent.Should().Be(20m);
    }

    [Fact]
    public void CalculateDeltaPercent_WithDecrease_ShouldUseAbsoluteValue()
    {
        var percent = ValuationCostAdjustmentPolicy.CalculateDeltaPercent(10m, 8m);

        percent.Should().Be(20m);
    }

    [Fact]
    public void ValidateApproval_WhenDeltaIsBelowThreshold_ShouldPassWithoutApprover()
    {
        var result = ValuationCostAdjustmentPolicy.ValidateApproval(10m, 11.99m, null);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateApproval_WhenDeltaIsExactlyThreshold_ShouldPassWithoutApprover()
    {
        var result = ValuationCostAdjustmentPolicy.ValidateApproval(10m, 12m, null);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateApproval_WhenDeltaExceedsThresholdAndNoApprover_ShouldFail()
    {
        var result = ValuationCostAdjustmentPolicy.ValidateApproval(10m, 12.01m, null);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
        result.Error.Should().Contain("Approval required");
    }

    [Fact]
    public void ValidateApproval_WhenDeltaExceedsThresholdAndApproverProvided_ShouldPass()
    {
        var result = ValuationCostAdjustmentPolicy.ValidateApproval(10m, 15m, "manager@example.com");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateApproval_WhenLargeDecreaseAndNoApprover_ShouldFail()
    {
        var result = ValuationCostAdjustmentPolicy.ValidateApproval(100m, 40m, null);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void ValidateApproval_WhenLargeDecreaseAndApproverProvided_ShouldPass()
    {
        var result = ValuationCostAdjustmentPolicy.ValidateApproval(100m, 40m, "manager@example.com");

        result.IsSuccess.Should().BeTrue();
    }

    private static AdjustValuationCostCommand CreateCommand(
        int itemId = 100,
        decimal newCost = 12.5m,
        string reason = "Supplier cost update")
    {
        return new AdjustValuationCostCommand
        {
            CommandId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            CausationId = Guid.NewGuid(),
            ItemId = itemId,
            NewCost = newCost,
            Reason = reason
        };
    }
}
