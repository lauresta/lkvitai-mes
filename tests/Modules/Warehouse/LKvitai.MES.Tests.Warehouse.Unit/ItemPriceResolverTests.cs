using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public sealed class ItemPriceResolverTests
{
    [Fact]
    public void Resolve_PrefersPositiveWac_ThenPurchase()
    {
        ItemPriceResolver.Resolve(12m, 8m).Should().Be((12m, "WAC"));
        ItemPriceResolver.Resolve(0m, 8m).Should().Be((8m, "Purchase"));
        ItemPriceResolver.Resolve(null, 8m).Should().Be((8m, "Purchase"));
        ItemPriceResolver.Resolve(0m, null).Should().Be((null, null));
    }

    [Fact]
    public void Value_RoundsAwayFromZero_AndPreservesMissingCost()
    {
        ItemPriceResolver.Value(1, 1.005m).Should().Be(1.01m);
        ItemPriceResolver.Value(-1, 1.005m).Should().Be(-1.01m);
        ItemPriceResolver.Value(5, null).Should().BeNull();
    }
}
