using LKvitai.MES.Modules.Sales.Application.OrderEntry;
using Xunit;

namespace LKvitai.MES.Tests.Sales.Auth;

public sealed class LegacyPriceEntryRulesTests
{
    [Theory]
    [InlineData(null, -1025240863, true)]
    [InlineData("any", -1025240863, true)]
    [InlineData("fixed", -1025240863, false)]
    [InlineData("FIXED", -1025240863, false)]
    [InlineData("fixed", 428417548, true)]
    [InlineData("fixed", 2112842479, true)]
    [InlineData("fixed", 2112842478, false)]
    public void Manual_price_support_follows_current_mode_and_exact_exception_types(
        string? categoryMode, int typeId, bool expected)
    {
        Assert.Equal(expected, LegacyPriceEntryRules.SupportsManualUnitPriceForType(typeId, categoryMode));
    }

    [Fact]
    public void Null_category_mode_is_any_without_inventing_a_mode_for_other_values()
    {
        Assert.Equal("any", LegacyPriceEntryRules.NormalizeMode(null));
        Assert.Equal("fixed", LegacyPriceEntryRules.NormalizeMode("fixed"));
        Assert.Equal("", LegacyPriceEntryRules.NormalizeMode(""));
    }
}
