using System.Globalization;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using LKvitai.MES.Modules.Sales.WebUI.Formatting;
using Xunit;

namespace LKvitai.MES.Tests.Sales.Auth;

public sealed class OrderDisplayFormattingTests
{
    [Theory]
    [InlineData("LTL", 1.0000, "EUR")]
    [InlineData("ltl", 1.0, "EUR")]
    [InlineData(" LTL ", 1.0, "EUR")]
    [InlineData("LTL", 3.4528, "LTL")]
    [InlineData("EUR", 1.0, "EUR")]
    [InlineData("LVL", 1.0, "LVL")]
    public void Legacy_base_currency_with_rate_one_is_shown_as_euro(string code, double rate, string expected)
        => Assert.Equal(expected, LegacyCurrencyLabel.Display(code, (decimal)rate));

    [Fact]
    public void Unknown_rate_or_missing_code_is_left_alone()
    {
        Assert.Equal("LTL", LegacyCurrencyLabel.Display("LTL", null));
        Assert.Null(LegacyCurrencyLabel.Display(null, 1m));
    }

    [Theory]
    [InlineData("1.000", "1")]
    [InlineData("0.0100", "0.01")]
    [InlineData("2.500", "2.5")]
    [InlineData("0.001", "0.001")]
    [InlineData("12345.67", "12345.67")]
    public void Quantity_has_at_most_three_decimals_without_trailing_zeros(string stored, string shown)
        => Assert.Equal(shown, OrderInputNumber.Quantity(decimal.Parse(stored, CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("170.0000", "170.00")]
    [InlineData("0.0100", "0.01")]
    [InlineData("9.990", "9.99")]
    [InlineData("0.0125", "0.0125")]
    [InlineData("1234.5", "1234.50")]
    public void Price_has_two_decimals_and_up_to_four_only_when_needed(string stored, string shown)
        => Assert.Equal(shown, OrderInputNumber.Price(decimal.Parse(stored, CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("0.0000000000", "0")]
    [InlineData("12.50", "12.5")]
    [InlineData("-5", "-5")]
    [InlineData("33.333", "33.33")]
    public void Discount_percent_has_at_most_two_decimals(string stored, string shown)
        => Assert.Equal(shown, OrderInputNumber.Percent(decimal.Parse(stored, CultureInfo.InvariantCulture)));

    [Theory]
    [InlineData("lt-LT")]
    [InlineData("en-GB")]
    public void Formatting_and_parsing_do_not_depend_on_the_current_culture(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            Assert.Equal("1234.50", OrderInputNumber.Price(1234.5m));
            Assert.Equal(12.5m, OrderInputNumber.Parse("12,5", 0m));
            Assert.Equal(12.5m, OrderInputNumber.Parse("12.5", 0m));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("1 234,56", 1234.56)]
    [InlineData("0,001", 0.001)]
    [InlineData("-3.5", -3.5)]
    [InlineData("170", 170)]
    public void Parse_accepts_comma_or_dot_as_decimal_mark(string typed, double expected)
        => Assert.Equal((decimal)expected, OrderInputNumber.Parse(typed, 99m));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("abc")]
    [InlineData("1.2.3")]
    [InlineData(null)]
    public void Parse_keeps_the_previous_value_when_the_text_is_not_a_number(string? typed)
        => Assert.Equal(7.25m, OrderInputNumber.Parse(typed, 7.25m));
}
