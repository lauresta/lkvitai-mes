using System.Globalization;

namespace LKvitai.MES.Modules.Finance.WebUI.IncomesReport;

/// <summary>
/// Lithuanian number formatting of the incomes report, independent of the UI language and the host culture:
/// money <c>1 240,00 €</c> (non-breaking spaces, comma decimals, U+2212 minus), percent <c>−10,5 %</c>.
/// </summary>
public static class IncomesFormat
{
    public const char Nbsp = ' ';
    public const char Minus = '−';
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Money with cents (KPIs, table, print, Excel).</summary>
    public static string Money(decimal value) => Number(value, 2) + Nbsp + "€";

    /// <summary>Money in whole euros (chart, matrix cells, comparison bases).</summary>
    public static string MoneyWhole(decimal value) => Number(value, 0) + Nbsp + "€";

    /// <summary>A count with non-breaking thousands.</summary>
    public static string Count(long value) => Number(value, 0);

    public static string Number(decimal value, int decimals)
    {
        var rounded = Math.Round(Math.Abs(value), decimals, MidpointRounding.AwayFromZero);
        var text = rounded.ToString(decimals == 0 ? "#,0" : "#,0." + new string('0', decimals), Invariant)
            .Replace(',', Nbsp).Replace('.', ',');
        return (value < 0 && rounded != 0 ? Minus.ToString() : "") + text;
    }

    /// <summary>Change in percent with its sign: <c>+5,2 %</c>, <c>−10,5 %</c>, <c>0,0 %</c>.</summary>
    public static string Signed(decimal percent)
    {
        var rounded = Math.Round(percent, 1, MidpointRounding.AwayFromZero);
        var sign = rounded > 0 ? "+" : rounded < 0 ? Minus.ToString() : "";
        return sign + Math.Abs(rounded).ToString("0.0", Invariant).Replace('.', ',') + Nbsp + "%";
    }

    public static string Percent(decimal percent, int decimals = 0) => Number(percent, decimals) + Nbsp + "%";

    /// <summary>Formats an invariant number from the API (e.g. a remark value) as a percent.</summary>
    public static string Percent(string invariant) =>
        decimal.TryParse(invariant, NumberStyles.Number, Invariant, out var value)
            ? Number(value, invariant.Contains('.') ? 1 : 0) + Nbsp + "%" : invariant;

    public static string Date(DateOnly date) => date.ToString("yyyy-MM-dd", Invariant);

    /// <summary>
    /// Lithuanian plural form: <c>One</c> for 1, 21, 31…; <c>Few</c> for 2–9, 22–29…; <c>Many</c> for 0 and 10–20, 30….
    /// Resource keys end with this suffix; English maps Few and Many to the same plural.
    /// </summary>
    public static string PluralForm(long count)
    {
        var last = Math.Abs(count) % 10;
        var lastTwo = Math.Abs(count) % 100;
        if (last == 1 && lastTwo != 11) return "One";
        return last is >= 2 and <= 9 && lastTwo is not (>= 12 and <= 19) ? "Few" : "Many";
    }

    /// <summary>Change of <paramref name="value"/> against <paramref name="baseline"/> in percent; null when there is no base.</summary>
    public static decimal? Change(decimal value, decimal baseline) =>
        baseline == 0 ? null : (value - baseline) / Math.Abs(baseline) * 100;

    public static bool IsWeekend(DateOnly day) => day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    /// <summary>A "nice" step for at most four gridlines above <paramref name="max"/>.</summary>
    public static decimal AxisStep(decimal max)
    {
        decimal[] steps = [100, 200, 250, 500, 1_000, 2_000, 2_500, 5_000, 10_000, 20_000, 25_000, 50_000, 100_000, 200_000,
            250_000, 500_000, 1_000_000, 2_000_000, 2_500_000, 5_000_000, 10_000_000];
        foreach (var step in steps)
            if (max / step <= 4) return step;
        return 25_000_000;
    }
}
