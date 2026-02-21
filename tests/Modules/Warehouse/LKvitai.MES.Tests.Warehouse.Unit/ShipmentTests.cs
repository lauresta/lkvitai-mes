using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

[Trait("Category", "OutboundOrders")]
public sealed class ShipmentTests
{
    [Fact]
    public void Pack_ShouldTransition_FromPackingToPacked()
    {
        var shipment = new Shipment();

        var result = shipment.Pack(DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Dispatch_ShouldFail_WhenNotPacked()
    {
        var shipment = new Shipment();

        var result = shipment.Dispatch(Carrier.FedEx, "TRACK-1", DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void DeliveryFlow_ShouldSucceed_AfterDispatchAndInTransit()
    {
        var shipment = new Shipment();
        shipment.Pack(DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        shipment.Dispatch(Carrier.FedEx, "TRACK-2", DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        shipment.MarkInTransit(DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();

        var result = shipment.ConfirmDelivery("signed", null, DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
    }
}
