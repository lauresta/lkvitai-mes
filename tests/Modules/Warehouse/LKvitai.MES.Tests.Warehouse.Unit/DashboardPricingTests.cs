using FluentAssertions;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public sealed class DashboardPricingTests
{
    private static readonly Dictionary<string, ItemPriceSnapshot> Prices = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SKU-A"] = new(1, "SKU-A", 10m, "WAC", 8m, 25m),
        ["SKU-B"] = new(2, "SKU-B", null, null, null, null)
    };

    [Fact]
    public void OverviewTotals_UseCaseInsensitiveSku_IgnoreNegativeRows_AndRequireCompletePrices()
    {
        var complete = DashboardPricing.Calculate(
            [("sku-a", 3m), ("SKU-A", -100m)], Prices);
        complete.Cost.Should().Be(30m);
        complete.Sale.Should().Be(75m);

        var incomplete = DashboardPricing.Calculate(
            [("sku-a", 3m), ("SKU-B", 2m), ("SKU-A", -100m)], Prices);
        incomplete.Cost.Should().BeNull();
        incomplete.Sale.Should().BeNull();
    }

    [Fact]
    public void CategoryAndWarehouseGroups_HaveSameCompleteAndIncompleteValueRule()
    {
        var pricedCategory = DashboardPricing.Calculate([("SKU-A", 2m)], Prices);
        var unpricedCategory = DashboardPricing.Calculate([("SKU-B", 1m)], Prices);
        pricedCategory.Cost.Should().Be(20m);
        unpricedCategory.Cost.Should().BeNull();

        var pricedWarehouse = DashboardPricing.Calculate([("SKU-A", 2m)], Prices);
        var mixedWarehouse = DashboardPricing.Calculate([("SKU-A", 2m), ("SKU-B", 1m)], Prices);
        pricedWarehouse.Cost.Should().Be(20m);
        mixedWarehouse.Cost.Should().BeNull();
        mixedWarehouse.Sale.Should().BeNull();
    }

    [Fact]
    public void PerRowValueRoundsAwayFromZero()
    {
        var prices = new Dictionary<string, ItemPriceSnapshot>(StringComparer.OrdinalIgnoreCase)
        {
            ["SKU-C"] = new(3, "SKU-C", 1.005m, "WAC", null, 2.005m)
        };
        var result = DashboardPricing.Calculate([("SKU-C", 1m)], prices);
        result.Cost.Should().Be(1.01m);
        result.Sale.Should().Be(2.01m);
    }
}
