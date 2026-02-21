using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

[Trait("Category", "Valuation")]
[Trait("Category", "Domain")]
public class CostAdjustmentRulesTests
{
    [Fact]
    public void ValidateRequest_WhenItemIdIsInvalid_ShouldFail()
    {
        var command = CreateCommand(itemId: 0);

        var result = CostAdjustmentRules.ValidateRequest(command);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void ValidateRequest_WhenCostIsNonPositive_ShouldFail()
    {
        var command = CreateCommand(newUnitCost: 0m);

        var result = CostAdjustmentRules.ValidateRequest(command);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void ValidateRequest_WhenReasonIsShort_ShouldFail()
    {
        var command = CreateCommand(reason: "too short");

        var result = CostAdjustmentRules.ValidateRequest(command);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void ValidateRequest_WhenValid_ShouldSucceed()
    {
        var command = CreateCommand();

        var result = CostAdjustmentRules.ValidateRequest(command);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateApproval_WhenImpactIsWithinThreshold_ShouldNotRequireApproval()
    {
        var result = CostAdjustmentRules.ValidateApproval(
            absoluteImpact: 1000m,
            approverId: null,
            hasManagerApproval: false,
            hasCfoApproval: false);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateApproval_WhenManagerApprovalMissing_ShouldFailValidation()
    {
        var result = CostAdjustmentRules.ValidateApproval(
            absoluteImpact: 1000.01m,
            approverId: null,
            hasManagerApproval: false,
            hasCfoApproval: false);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
        result.Error.Should().Contain("Manager approval required");
    }

    [Fact]
    public void ValidateApproval_WhenCfoApprovalMissingAndNoApprover_ShouldFailValidation()
    {
        var result = CostAdjustmentRules.ValidateApproval(
            absoluteImpact: 10000.01m,
            approverId: null,
            hasManagerApproval: false,
            hasCfoApproval: false);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
        result.Error.Should().Contain("CFO approval required");
    }

    [Fact]
    public void ValidateApproval_WhenManagerRoleMissing_ShouldFailForbidden()
    {
        var result = CostAdjustmentRules.ValidateApproval(
            absoluteImpact: 2000m,
            approverId: Guid.NewGuid(),
            hasManagerApproval: false,
            hasCfoApproval: false);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.Forbidden);
    }

    [Fact]
    public void ValidateApproval_WhenManagerRolePresent_ShouldSucceed()
    {
        var result = CostAdjustmentRules.ValidateApproval(
            absoluteImpact: 2000m,
            approverId: Guid.NewGuid(),
            hasManagerApproval: true,
            hasCfoApproval: false);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateApproval_WhenCfoRoleMissingForLargeImpact_ShouldFailForbidden()
    {
        var result = CostAdjustmentRules.ValidateApproval(
            absoluteImpact: 20000m,
            approverId: Guid.NewGuid(),
            hasManagerApproval: true,
            hasCfoApproval: false);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.Forbidden);
        result.Error.Should().Contain("CFO approval required");
    }

    [Fact]
    public void ValidateApproval_WhenCfoRolePresentForLargeImpact_ShouldSucceed()
    {
        var result = CostAdjustmentRules.ValidateApproval(
            absoluteImpact: 20000m,
            approverId: Guid.NewGuid(),
            hasManagerApproval: true,
            hasCfoApproval: true);

        result.IsSuccess.Should().BeTrue();
    }

    private static AdjustCostCommand CreateCommand(
        int itemId = 1,
        decimal newUnitCost = 10m,
        string reason = "Vendor price increase")
    {
        return new AdjustCostCommand
        {
            CommandId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            CausationId = Guid.NewGuid(),
            ItemId = itemId,
            NewUnitCost = newUnitCost,
            Reason = reason
        };
    }
}
