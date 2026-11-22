using System.Globalization;

namespace LKvitai.MES.Modules.Sales.WebUI.Formatting;

/// <summary>
/// Text shown inside the order-entry <c>type="number"</c> inputs and the parser that reads it back.
/// Browsers always exchange number-input values with a dot, whatever the UI language, so the text
/// is formatted with the invariant culture. This only trims what is displayed (legacy values arrive
/// as 170.0000 / 0.0100); the stored decimal is never rounded here.
/// </summary>
public static class OrderInputNumber
{
    /// <summary>Quantity: up to 3 decimals, no trailing zeros ("1", "0.01", "2.5").</summary>
    public static string Quantity(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Price: 2 decimals, up to 4 only when the value needs them ("170.00", "9.99", "0.0125").</summary>
    public static string Price(decimal value) => value.ToString("0.00##", CultureInfo.InvariantCulture);

    /// <summary>Percent: up to 2 decimals, no trailing zeros ("0", "12.5").</summary>
    public static string Percent(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads what the user typed. A comma or a dot works as the decimal mark, spaces are ignored.
    /// Anything unreadable keeps the previous value.
    /// </summary>
    public static decimal Parse(object? raw, decimal fallback)
    {
        var text = raw?.ToString();
        if (string.IsNullOrWhiteSpace(text)) return fallback;

        var cleaned = text.Replace(" ", "").Replace(" ", "").Replace(" ", "").Replace(',', '.');
        return decimal.TryParse(cleaned, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }
}
