using LKvitai.MES.Modules.Finance.Api.BankPayments;
using Xunit;

namespace LKvitai.MES.Tests.Finance.Unit;

public class BankPasteParserTests
{
    [Theory]
    [InlineData("1,000.00 €", 1000)]
    [InlineData("1 020,01", 1020.01)]
    [InlineData("634.14", 634.14)]
    [InlineData("1.020,01 EUR", 1020.01)]
    public void Money_formats_parse(string text, decimal expected)
    {
        Assert.True(BankPasteParser.TryMoney(text, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("2026.10.30")]
    [InlineData("30.10.2026")]
    [InlineData("2026-10-30")]
    public void Date_formats_parse(string text)
    {
        Assert.True(BankPasteParser.TryDate(text, out var actual));
        Assert.Equal(new DateOnly(2026, 10, 30), actual);
    }

    [Fact]
    public void Excel_rows_strip_legacy_number_and_keep_purpose()
    {
        var parsed = BankPasteParser.Parse("79783\t2026.10.30\tUAB Pavyzdys\tUžsakymas VV-123\t1 020,01 €\nbad line");
        Assert.Equal(2, parsed.Count);
        Assert.Equal("UAB Pavyzdys", parsed[0].Firm);
        Assert.Equal("Užsakymas VV-123", parsed[0].Purpose);
        Assert.Equal(1020.01m, parsed[0].Sum);
        Assert.Equal("New", parsed[0].Status);
        Assert.Equal("NoColumns", parsed[1].Error);
    }
}
