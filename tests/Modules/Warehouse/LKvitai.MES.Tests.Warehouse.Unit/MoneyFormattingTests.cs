using System.Globalization;
using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public sealed class MoneyFormattingTests
{
    [Theory]
    [InlineData("en", "1,234.50 €", "-123,456,790.00 €", "0.0045 €")]
    [InlineData("lt", "1 234,50 €", "-123 456 790,00 €", "0,0045 €")]
    public void UiCultureControlsMoneyWithoutChangingTransportCulture(string language, string amount, string negative, string unit)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
            Money.Format(1234.5m).Should().Be(amount);
            Money.Format(-123456789.995m).Should().Be(negative);
            Money.FormatUnit(0.0045m).Should().Be(unit);
            Money.Format(null).Should().Be("–");
            Money.FormatUnit(null).Should().Be("–");
            1234.50m.ToString("0.00", CultureInfo.InvariantCulture).Should().Be("1234.50");
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }
}
