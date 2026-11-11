using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public sealed class PhysicalLocationTests
{
    [Theory]
    [InlineData("BIN", false, true)]
    [InlineData("BIN", true, false)]
    [InlineData("AGNUM", false, false)]
    [InlineData("agnum", false, false)]
    [InlineData("IN_TRANSIT_TRF-1", false, false)]
    [InlineData("in_transit_1", false, false)]
    [InlineData("RECEIVING", true, false)]
    public void PhysicalStockRequiresKnownNonVirtualLocation(string code, bool virtualLocation, bool expected)
    {
        PhysicalLocation.IsPhysical(new Location { Code = code, IsVirtual = virtualLocation }).Should().Be(expected);
        PhysicalLocation.IsPhysical(null).Should().BeFalse();
    }
}
