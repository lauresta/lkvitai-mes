using System.Globalization;
using LKvitai.MES.Modules.Sales.Contracts.Orders;

namespace LKvitai.MES.Modules.Sales.WebUI.Formatting;

/// <summary>
/// Lithuanian-locale display formatters for the Sales WebUI. Matches the S-0
/// sample grammar 1:1 — narrow no-break space (U+202F) between thousands,
/// no-break space (U+00A0) between number and currency, comma decimal.
/// Centralised so Razor pages stay free of localization details.
/// </summary>
public static class OrderFormat
{
    private const string NarrowNoBreakSpace = "\u202F";
    private const string NoBreakSpace       = "\u00A0";

    // Money and percent share the Lithuanian numeric grammar: NNBSP thousands
    // separator, comma decimal, group of 3. Only the trailing unit differs,
    // so we keep a single shared NumberFormatInfo instead of two clones.
    private static readonly NumberFormatInfo NumberFormat = BuildNumberFormat();

    public static string Money(decimal value)
        => value.ToString("N2", NumberFormat) + NoBreakSpace + "\u20AC";

    public static string Percent(decimal value)
        => value.ToString("N2", NumberFormat) + NoBreakSpace + "%";

    public static string Date(DateOnly value)
        => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Date(DateOnly? value)
        => value is null ? string.Empty : Date(value.Value);

    public static string DateTime(DateTime value)
        => value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    public static string Dimension(decimal? value)
        => value is null ? string.Empty : value.Value.ToString("N2", NumberFormat);

    public static string Quantity(decimal value)
        => value.ToString("N2", NumberFormat);

    public static string Integer(int value)
        => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Maps debt amount + overdue flag to the matching <c>is-debt-*</c> tier class
    /// from <c>docs/ux/sales-orders-codex-rules.md</c> §3.
    /// </summary>
    public static string DebtClass(decimal debt, bool isOverdue)
    {
        if (debt <= 0m) return "is-debt-zero";
        return isOverdue ? "is-debt-overdue" : "is-debt-pos";
    }

    /// <summary>Returns the chip modifier class for the supplied semantic status code.</summary>
    public static string StatusChip(string statusCode)
        => string.IsNullOrWhiteSpace(statusCode) ? "chip--entered" : $"chip--{statusCode}";

    /// <summary>Returns the duty dot class for an employee row (<c>duty--sales</c> etc).</summary>
    public static string DutyClass(string dutyCode)
        => string.IsNullOrWhiteSpace(dutyCode) ? "duty--sales" : $"duty--{dutyCode}";

    /// <summary>
    /// Maps an <see cref="OrderAmountKind"/> to the matching <c>amount-card--*</c>
    /// modifier. Empty string for the cards that have no accent.
    /// </summary>
    public static string AmountModifier(OrderAmountKind kind) => kind switch
    {
        OrderAmountKind.AfterDiscount => "amount-card--total",
        OrderAmountKind.Paid          => "amount-card--paid",
        OrderAmountKind.Debt          => "amount-card--debt",
        _ => string.Empty,
    };

    /// <summary>
    /// Renders the value half of an Amounts card: money for most kinds, percent
    /// for <see cref="OrderAmountKind.Discount"/>. Falls back to an em dash when
    /// the matching field is null.
    /// </summary>
    public static string AmountValue(OrderAmountDto amount)
    {
        ArgumentNullException.ThrowIfNull(amount);
        return amount.Kind switch
        {
            OrderAmountKind.Discount =>
                amount.Percent is null ? "—" : Percent(amount.Percent.Value),
            _ =>
                amount.Amount is null ? "—" : Money(amount.Amount.Value),
        };
    }

    private static NumberFormatInfo BuildNumberFormat()
    {
        var nfi = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        nfi.NumberGroupSeparator   = NarrowNoBreakSpace;
        nfi.NumberDecimalSeparator = ",";
        nfi.NumberGroupSizes       = new[] { 3 };
        return nfi;
    }
}
