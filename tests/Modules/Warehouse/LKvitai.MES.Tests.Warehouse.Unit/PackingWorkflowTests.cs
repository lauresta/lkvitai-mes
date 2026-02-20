using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.SharedKernel;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

[Trait("Category", "Packing")]
public sealed class PackingWorkflowTests
{
    [Fact]
    public void Pack_ShouldFail_WhenOrderNotPicked()
    {
        var order = new OutboundOrder();
        order.MarkAllocated(Guid.NewGuid()).IsSuccess.Should().BeTrue();

        var result = order.Pack(Guid.NewGuid(), DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    public void Pack_ShouldSucceed_WhenOrderPicked()
    {
        var order = new OutboundOrder();
        order.MarkAllocated(Guid.NewGuid()).IsSuccess.Should().BeTrue();
        order.StartPicking().IsSuccess.Should().BeTrue();
        order.CompletePicking(DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();

        var result = order.Pack(Guid.NewGuid(), DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
    }
}
