using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public sealed class MoneyFormattingTests
{
    [Fact]
    public void AmountsRoundToCentsAndUnitCostsPreserveFourDecimals()
    {
        Money.Format(1.005m).Should().Be("1.01 €");
        Money.Format(-1.005m).Should().Be("-1.01 €");
        Money.Format(-123456789.995m).Should().Be("-123456790.00 €");
        Money.Format(123456789.995m).Should().Be("123456790.00 €");
        Money.Format(null).Should().Be("–");
        Money.FormatUnit(0.0045m).Should().Be("0.0045 €");
        Money.FormatUnit(12m).Should().Be("12.00 €");
        Money.FormatUnit(null).Should().Be("–");
    }
}
