using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

[Trait("Category", "Valuation")]
[Trait("Category", "Domain")]
public class ValuationWriteDownPolicyTests
{
    [Fact]
    public void ValidateRequest_WithValidPayload_ShouldPass()
    {
        var result = ValuationWriteDownPolicy.ValidateRequest(CreateCommand(), currentValue: 5000m);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateRequest_WithInvalidItemId_ShouldFail()
    {
        var result = ValuationWriteDownPolicy.ValidateRequest(CreateCommand(itemId: 0), currentValue: 5000m);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void ValidateRequest_WithNegativeNewValue_ShouldFail()
    {
        var result = ValuationWriteDownPolicy.ValidateRequest(CreateCommand(newValue: -1m), currentValue: 5000m);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void ValidateRequest_WhenNewValueEqualsCurrent_ShouldFail()
    {
        var result = ValuationWriteDownPolicy.ValidateRequest(CreateCommand(newValue: 5000m), currentValue: 5000m);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("must reduce value");
    }

    [Fact]
    public void ValidateRequest_WhenNewValueGreaterThanCurrent_ShouldFail()
    {
        var result = ValuationWriteDownPolicy.ValidateRequest(CreateCommand(newValue: 6000m), currentValue: 5000m);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("must reduce value");
    }

    [Fact]
    public void ValidateRequest_WithMissingReason_ShouldFail()
    {
        var result = ValuationWriteDownPolicy.ValidateRequest(CreateCommand(reason: " "), currentValue: 5000m);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void ValidateApproval_WhenDeltaBelowThreshold_ShouldPassWithoutApprover()
    {
        var result = ValuationWriteDownPolicy.ValidateApproval(2000m, 1200m, null, false);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateApproval_WhenDeltaEqualsThreshold_ShouldPassWithoutApprover()
    {
        var result = ValuationWriteDownPolicy.ValidateApproval(3000m, 2000m, null, false);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateApproval_WhenDeltaAboveThresholdAndApproverMissing_ShouldFail()
    {
        var result = ValuationWriteDownPolicy.ValidateApproval(5000m, 3500m, null, true);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void ValidateApproval_WhenDeltaAboveThresholdAndRoleMissing_ShouldFailForbidden()
    {
        var result = ValuationWriteDownPolicy.ValidateApproval(5000m, 3500m, "manager@example.com", false);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.Forbidden);
    }

    [Fact]
    public void ValidateApproval_WhenDeltaAboveThresholdAndRolePresent_ShouldPass()
    {
        var result = ValuationWriteDownPolicy.ValidateApproval(5000m, 3500m, "manager@example.com", true);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateApproval_WhenDeltaHugeAndRoleMissing_ShouldFailForbidden()
    {
        var result = ValuationWriteDownPolicy.ValidateApproval(10000m, 1000m, "manager@example.com", false);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.Forbidden);
    }

    [Fact]
    public void ValidateApproval_WhenDeltaHugeAndApproverMissing_ShouldFailValidation()
    {
        var result = ValuationWriteDownPolicy.ValidateApproval(10000m, 1000m, null, true);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void ValidateApproval_WhenDeltaHugeAndApproverRolePresent_ShouldPass()
    {
        var result = ValuationWriteDownPolicy.ValidateApproval(10000m, 1000m, "manager@example.com", true);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateApproval_WhenDeltaIsNegative_ShouldTreatAsReductionMagnitude()
    {
        var result = ValuationWriteDownPolicy.ValidateApproval(1000m, -500m, "manager@example.com", true);

        result.IsSuccess.Should().BeTrue();
    }

    private static WriteDownCommand CreateCommand(
        int itemId = 10,
        decimal newValue = 3000m,
        string reason = "Damage during handling")
    {
        return new WriteDownCommand
        {
            CommandId = Guid.NewGuid(),
            CorrelationId = Guid.NewGuid(),
            CausationId = Guid.NewGuid(),
            ItemId = itemId,
            NewValue = newValue,
            Reason = reason
        };
    }
}
