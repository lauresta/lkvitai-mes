using LKvitai.MES.Modules.Sales.Application.Pricing;
using LKvitai.MES.Modules.Sales.Contracts.Quotes;
using LKvitai.MES.Modules.Sales.Infrastructure.Sql;
using Xunit;

namespace LKvitai.MES.Tests.Sales.Auth;

public sealed class SalesUnitPricePolicyTests
{
    [Fact]
    public void Found_price_preserves_four_decimal_money_including_duplicate_identical_matches()
    {
        var one = SalesUnitPricePolicy.Resolve(true, true, 1, 1, 0, 20.8526m, 20.8526m);
        var duplicate = SalesUnitPricePolicy.Resolve(true, true, 3, 1, 0, 20.8526m, 20.8526m);
        Assert.Equal(SalesUnitPriceStatus.Found, one.Status);
        Assert.Equal(20.8526m, one.UnitPrice);
        Assert.Equal(one.UnitPrice, duplicate.UnitPrice);
        Assert.Equal(3, duplicate.MatchingRows);
    }

    [Fact]
    public void Conflicting_candidates_do_not_pick_cheapest_nearest_or_sentinel_price()
    {
        var result = SalesUnitPricePolicy.Resolve(true, true, 2, 2, 0, 20.8526m, 999m);
        Assert.Equal(SalesUnitPriceStatus.Ambiguous, result.Status);
        Assert.Null(result.UnitPrice);
        Assert.Equal(20.8526m, result.MinimumMatchedPrice);
        Assert.Equal(999m, result.MaximumMatchedPrice);
    }

    [Fact]
    public void Missing_reference_missing_price_and_null_price_are_distinct_outcomes()
    {
        Assert.Equal(SalesUnitPriceStatus.InvalidReference,
            SalesUnitPricePolicy.Resolve(false, true, 0, 0, 0, null, null).Status);
        Assert.Equal(SalesUnitPriceStatus.InvalidReference,
            SalesUnitPricePolicy.Resolve(true, false, 0, 0, 0, null, null).Status);
        var missing = SalesUnitPricePolicy.Resolve(true, true, 0, 0, 0, null, null);
        Assert.Equal(SalesUnitPriceStatus.Missing, missing.Status);
        Assert.Null(missing.UnitPrice);
        var nullRow = SalesUnitPricePolicy.Resolve(true, true, 2, 1, 1, 20.8526m, 20.8526m);
        Assert.Equal(SalesUnitPriceStatus.InvalidPriceData, nullRow.Status);
        Assert.Null(nullRow.UnitPrice);
        Assert.Equal(SalesUnitPriceStatus.Found,
            SalesUnitPricePolicy.Resolve(true, true, 1, 1, 0, 0m, 0m).Status);
    }

    [Fact]
    public void Signed_ids_small_positive_dimensions_and_fractional_quantity_are_not_demo_restricted()
    {
        SalesUnitPricePolicy.Validate(new SalesUnitPriceQuery(-1025240863, -123, 1, 199, .001m, new DateOnly(2026, 10, 31)));
    }

    [Theory]
    [InlineData(0, 1500, 1)]
    [InlineData(800, -1, 1)]
    [InlineData(800, 1500, 0)]
    [InlineData(800, 1500, -1)]
    public async Task Invalid_required_values_are_rejected_before_database_access(int width, int height, int quantity)
    {
        var adapter = new SqlSalesUnitPriceLookup(new SalesSqlOptions { ConnectionString = "Server=unused.invalid;Database=none" },new RejectingLegacySqlConnectionFactory());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => adapter.GetUnitPriceAsync(
            new SalesUnitPriceQuery(-1025240863, 2328, width, height, quantity, new DateOnly(2026, 10, 31)), default));
    }

    [Fact]
    public void Sql_datetime_range_is_validated_without_normalizing_order_date()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SalesUnitPricePolicy.Validate(
            new SalesUnitPriceQuery(-1025240863, 2328, 800, 1500, 1, new DateOnly(1752, 12, 31))));
        SalesUnitPricePolicy.Validate(new SalesUnitPriceQuery(-1025240863, 2328, 800, 1500, 1, new DateOnly(1753, 1, 1)));
    }
}
