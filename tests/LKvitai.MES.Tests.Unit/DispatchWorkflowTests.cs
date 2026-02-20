using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.SharedKernel;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

[Trait("Category", "Dispatch")]
public sealed class DispatchWorkflowTests
{
    [Fact]
    public void Dispatch_ShouldFail_WhenShipmentNotPacked()
    {
        var shipment = new Shipment();

        var result = shipment.Dispatch(Carrier.FedEx, "TRACK-001", DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void Dispatch_ShouldSucceed_AfterPack()
    {
        var shipment = new Shipment();
        shipment.Pack(DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();

        var result = shipment.Dispatch(Carrier.FedEx, "TRACK-002", DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
    }
}
