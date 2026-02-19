using FluentAssertions;
using LKvitai.MES.Domain.Entities;
using LKvitai.MES.SharedKernel;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

[Trait("Category", "OutboundOrders")]
public sealed class OutboundOrderTests
{
    [Fact]
    public void StartPicking_ShouldTransition_FromAllocatedToPicking()
    {
        var order = CreateAllocatedOrder();

        var result = order.StartPicking();

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void CompletePicking_ShouldFail_WhenStatusIsNotPicking()
    {
        var order = CreateAllocatedOrder();

        var result = order.CompletePicking(DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void Pack_ShouldSetShipmentId_AndStatusPacked()
    {
        var shipmentId = Guid.NewGuid();
        var order = CreateAllocatedOrder();
        order.StartPicking();
        order.CompletePicking(DateTimeOffset.UtcNow);

        var result = order.Pack(shipmentId, DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Cancel_ShouldReject_WhenAlreadyDelivered()
    {
        var order = CreateAllocatedOrder();
        order.StartPicking();
        order.CompletePicking(DateTimeOffset.UtcNow);
        order.Pack(Guid.NewGuid(), DateTimeOffset.UtcNow);
        order.Ship(DateTimeOffset.UtcNow);
        order.ConfirmDelivery(DateTimeOffset.UtcNow);

        var result = order.Cancel("cancel after delivery");

        result.IsSuccess.Should().BeFalse();
    }

    private static OutboundOrder CreateAllocatedOrder()
    {
        var order = new OutboundOrder();
        order.MarkAllocated(Guid.NewGuid()).IsSuccess.Should().BeTrue();
        return order;
    }
}
