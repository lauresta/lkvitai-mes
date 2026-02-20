using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.SharedKernel;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

[Trait("Category", "SalesOrders")]
public sealed class SalesOrderTests
{
    [Fact]
    public void Submit_FromDraft_WithoutApproval_ShouldAllocate()
    {
        var order = BuildOrder();

        var result = order.Submit(requiresApproval: false);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(SalesOrderStatus.Allocated);
    }

    [Fact]
    public void Ship_FromDraft_ShouldFail()
    {
        var order = BuildOrder();

        var result = order.Ship(DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
        order.Status.Should().Be(SalesOrderStatus.Draft);
    }

    [Fact]
    public void FullHappyPath_ShouldReachInvoiced()
    {
        var order = BuildOrder();

        order.Submit(requiresApproval: false).IsSuccess.Should().BeTrue();
        order.Release().IsSuccess.Should().BeTrue();
        order.Pack(Guid.NewGuid()).IsSuccess.Should().BeTrue();
        order.Ship(DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        order.ConfirmDelivery(DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        order.Invoice(DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();

        order.Status.Should().Be(SalesOrderStatus.Invoiced);
    }

    [Fact]
    public void RecalculateTotals_ShouldUpdateLineAndOrderAmounts()
    {
        var order = BuildOrder();
        order.Lines.Add(new SalesOrderLine
        {
            SalesOrderId = order.Id,
            ItemId = 1,
            OrderedQty = 3m,
            UnitPrice = 10m
        });
        order.Lines.Add(new SalesOrderLine
        {
            SalesOrderId = order.Id,
            ItemId = 2,
            OrderedQty = 2m,
            UnitPrice = 7.5m
        });

        order.RecalculateTotals();

        order.TotalAmount.Should().Be(45m);
        order.Lines.Sum(x => x.LineAmount).Should().Be(45m);
    }

    [Fact]
    public void Cancel_WithoutReason_ShouldFailValidation()
    {
        var order = BuildOrder();

        var result = order.Cancel(string.Empty);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    private static SalesOrder BuildOrder()
    {
        return new SalesOrder
        {
            CustomerId = Guid.NewGuid(),
            Lines = new List<SalesOrderLine>(),
            ShippingAddress = new Address
            {
                Street = "Street 1",
                City = "Vilnius",
                State = "LT",
                ZipCode = "10000",
                Country = "LT"
            }
        };
    }
}
