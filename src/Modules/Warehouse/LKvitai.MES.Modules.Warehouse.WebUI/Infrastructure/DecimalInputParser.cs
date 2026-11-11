using System.Globalization;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;

/// <summary>
/// Parses operator-typed decimals independent of UI culture: both '.' and ',' are accepted as the decimal
/// separator, spaces / NBSP are treated as thousands separators.
/// </summary>
public static class DecimalInputParser
{
    public static bool TryParse(string? text, out decimal? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var cleaned = text.Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace('−', '-');

        var lastDot = cleaned.LastIndexOf('.');
        var lastComma = cleaned.LastIndexOf(',');
        var decimalIndex = Math.Max(lastDot, lastComma);

        string normalized;
        if (decimalIndex < 0)
        {
            normalized = cleaned;
        }
        else
        {
            // The right-most separator is the decimal one; any earlier '.' or ',' are grouping separators.
            var integerPart = cleaned[..decimalIndex].Replace(".", string.Empty, StringComparison.Ordinal).Replace(",", string.Empty, StringComparison.Ordinal);
            var fraction = cleaned[(decimalIndex + 1)..];
            normalized = $"{integerPart}.{fraction}";
        }

        if (decimal.TryParse(
                normalized,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }
}
