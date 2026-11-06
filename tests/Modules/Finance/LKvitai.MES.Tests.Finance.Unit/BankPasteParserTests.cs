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

    [Fact]
    public void Screenshot_name_errors_do_not_make_existing_bank_payments_new()
    {
        var date = new DateOnly(2026, 10, 30);
        var existing = new[]
        {
            new BankPaymentRow(79755, date, "Šiaulių r. Kuršėnų lopšelis-darželis Žiedelis", "", 330m),
            new BankPaymentRow(79757, date, "Vida Rudzenskienė", "", 225m)
        };
        var screenshot = BankPasteParser.Parse("2026-10-30\tŠiaulių r. Kuršėnų lopšelis-darželis\tApmokėta\t330,00 €\n" +
            "2026-10-30\tVida Rudženskienė\tIšankstinio mokėjimo sąskaita\t225,00 €");

        var checkedRows = BankPasteParser.MarkDuplicates(screenshot, existing, false);

        Assert.Equal(new[] { "PossibleDuplicate", "PossibleDuplicate" }, checkedRows.Select(x => x.Status));
        Assert.Equal(new int?[] { 79755, 79757 }, checkedRows.Select(x => x.MatchedNr));
        Assert.All(checkedRows, x => Assert.False(x.Selected));
    }

    [Fact]
    public void Exact_match_is_duplicate_and_different_date_is_new()
    {
        var date = new DateOnly(2026, 10, 30);
        var existing = new[] { new BankPaymentRow(79757, date, "Vida Rudzenskienė", "", 225m) };
        var rows = BankPasteParser.Parse("2026-10-30\tVida Rudzenskienė\tRef\t225,00 €\n" +
            "2026-10-31\tVida Rudzenskienė\tRef\t225,00 €");

        var checkedRows = BankPasteParser.MarkDuplicates(rows, existing, false);

        Assert.Equal("Duplicate", checkedRows[0].Status);
        Assert.Equal(79757, checkedRows[0].MatchedNr);
        Assert.Equal("New", checkedRows[1].Status);
        Assert.True(checkedRows[1].Selected);
    }
}
