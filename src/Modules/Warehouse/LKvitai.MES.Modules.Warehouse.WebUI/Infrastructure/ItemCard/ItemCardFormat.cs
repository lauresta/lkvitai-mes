using System.Globalization;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure.ItemCard;

/// <summary>
/// Number, money and date formatting of the item card: "412,5" (culture decimal separator), "4,80 €", "−18,0"
/// with U+2212 for negatives. Every method takes the culture explicitly; the overloads without one use the UI culture.
/// </summary>
public static class ItemCardFormat
{
    public const string Minus = "−";
    public const string Dash = "—";

    private const string QtyPattern = "#,##0.0##";
    private const string WholePattern = "#,##0.###";

    public static string Qty(decimal value) => Qty(value, CultureInfo.CurrentUICulture);

    public static string Qty(decimal value, CultureInfo culture)
        => Round(value, 3).ToString(QtyPattern, Display(culture));

    /// <summary>Quantity without trailing zeros ("150"), as used for the minimum quantity.</summary>
    public static string Whole(decimal? value) => Whole(value, CultureInfo.CurrentUICulture);

    public static string Whole(decimal? value, CultureInfo culture)
        => value.HasValue ? Round(value.Value, 3).ToString(WholePattern, Display(culture)) : Dash;

    /// <summary>"+300,0", "−18,0", or "0,0" (no sign for zero).</summary>
    public static string Signed(decimal value) => Signed(value, CultureInfo.CurrentUICulture);

    public static string Signed(decimal value, CultureInfo culture)
    {
        var magnitude = Qty(Math.Abs(value), culture);
        return value switch
        {
            > 0m => "+" + magnitude,
            < 0m => Minus + magnitude,
            _ => magnitude
        };
    }

    public static string Money(decimal? value) => Money(value, CultureInfo.CurrentUICulture);

    public static string Money(decimal? value, CultureInfo culture)
        => value.HasValue
            ? Round(value.Value, 2).ToString("#,##0.00", Display(culture)) + " €"
            : Dash;

    /// <summary>"57,5 %" with one decimal.</summary>
    public static string Percent(decimal? value) => Percent(value, CultureInfo.CurrentUICulture);

    public static string Percent(decimal? value, CultureInfo culture)
        => value.HasValue ? Round(value.Value, 1).ToString("0.0", Display(culture)) + " %" : Dash;

    /// <summary>Lower-case unit for UI text ("M" → "m").</summary>
    public static string Unit(string? baseUoM)
        => string.IsNullOrWhiteSpace(baseUoM) ? string.Empty : baseUoM.Trim().ToLowerInvariant();

    /// <summary>Quantity followed by the unit: "412,5 m".</summary>
    public static string QtyWithUnit(decimal value, string? baseUoM, CultureInfo culture)
        => Join(Qty(value, culture), Unit(baseUoM));

    public static string QtyWithUnit(decimal value, string? baseUoM)
        => QtyWithUnit(value, baseUoM, CultureInfo.CurrentUICulture);

    /// <summary>Column title with the unit: "Kiekis, m"; just the title when the item has no unit.</summary>
    public static string ColumnTitle(string title, string? baseUoM)
    {
        var unit = Unit(baseUoM);
        return string.IsNullOrEmpty(unit) ? title : $"{title}, {unit}";
    }

    public static string WholeWithUnit(decimal? value, string? baseUoM)
        => value.HasValue ? Join(Whole(value), Unit(baseUoM)) : Dash;

    public static string Timestamp(DateTimeOffset value)
        => value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>API timestamps without an offset are UTC.</summary>
    public static string Timestamp(DateTime value) => Timestamp(AsOffset(value));

    public static string ShortTimestamp(DateTimeOffset value)
        => value.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);

    public static string ShortTimestamp(DateTime value) => ShortTimestamp(AsOffset(value));

    public static DateTimeOffset AsOffset(DateTime value)
        => value.Kind == DateTimeKind.Unspecified
            ? new DateTimeOffset(System.DateTime.SpecifyKind(value, DateTimeKind.Utc))
            : new DateTimeOffset(value);

    public static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Date(DateTime value)
        => AsOffset(value).ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string ShortDate(DateOnly value) => value.ToString("MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Culture used for display numbers: decimal separator of the culture, space as group separator in Lithuanian, U+2212 as minus.</summary>
    public static NumberFormatInfo Display(CultureInfo culture)
    {
        var format = (NumberFormatInfo)culture.NumberFormat.Clone();
        format.NegativeSign = Minus;
        if (culture.TwoLetterISOLanguageName == "lt")
        {
            format.NumberGroupSeparator = " ";
        }

        return format;
    }

    private static decimal Round(decimal value, int decimals)
        => decimal.Round(value, decimals, MidpointRounding.AwayFromZero);

    private static string Join(string value, string unit)
        => string.IsNullOrEmpty(unit) ? value : value + " " + unit;
}
