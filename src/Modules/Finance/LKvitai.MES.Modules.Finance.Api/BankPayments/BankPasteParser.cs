using System.Globalization;
using System.Text.RegularExpressions;

namespace LKvitai.MES.Modules.Finance.Api.BankPayments;

public sealed record BankPasteRow(int Line, DateOnly? Date, string Firm, string Purpose, decimal? Sum,
    string Status, string? Error, string Raw, bool Selected = false);

public static partial class BankPasteParser
{
    [GeneratedRegex(@"^\d{4}[./-]\d{1,2}[./-]\d{1,2}$|^\d{1,2}[./-]\d{1,2}[./-]\d{4}$")]
    private static partial Regex DateShape();
    [GeneratedRegex(@"^\d{4,6}$")]
    private static partial Regex LegacyNumber();

    public static IReadOnlyList<BankPasteRow> Parse(string text)
    {
        if (text.Length > 500_000) throw new ArgumentException("Paste is too large.");
        var output = new List<BankPasteRow>();
        var lines = text.Split('\n');
        if (lines.Length > 1500) throw new ArgumentException("Too many lines.");
        for (var lineNo = 0; lineNo < lines.Length; lineNo++)
        {
            var raw = lines[lineNo].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(raw)) continue;
            if (!raw.Contains('\t')) { output.Add(Error(lineNo + 1, raw, "NoColumns")); continue; }
            var cells = raw.Split('\t').Select(x => x.Trim()).ToList();
            var dateIndex = cells.FindIndex(x => TryDate(x, out _));
            if (dateIndex < 0) { output.Add(Error(lineNo + 1, raw, "NoDate")); continue; }
            TryDate(cells[dateIndex], out var date);
            cells.RemoveAt(dateIndex);
            var sumIndex = -1;
            decimal sum = 0;
            for (var i = cells.Count - 1; i >= 0; i--)
                if (TryMoney(cells[i], out sum)) { sumIndex = i; break; }
            if (sumIndex < 0) { output.Add(Error(lineNo + 1, raw, "NoSum")); continue; }
            cells.RemoveAt(sumIndex);
            while (cells.Count > 0 && string.IsNullOrWhiteSpace(cells[0])) cells.RemoveAt(0);
            if (cells.Count >= 3 && LegacyNumber().IsMatch(cells[0])) cells.RemoveAt(0);
            while (cells.Count > 0 && string.IsNullOrWhiteSpace(cells[0])) cells.RemoveAt(0);
            if (cells.Count == 0) { output.Add(Error(lineNo + 1, raw, "NoFirm")); continue; }
            var firm = cells[0];
            var purpose = string.Join(' ', cells.Skip(1).Where(x => !string.IsNullOrWhiteSpace(x)));
            if (firm.Length > 255 || purpose.Length > 1024 || sum == 0)
            { output.Add(Error(lineNo + 1, raw, "InvalidValue")); continue; }
            output.Add(new BankPasteRow(lineNo + 1, date, firm, purpose, sum, "New", null, raw, true));
        }
        return output;
    }

    private static BankPasteRow Error(int line, string raw, string reason) =>
        new(line, null, "", "", null, "Error", reason, raw);

    public static bool TryDate(string value, out DateOnly date)
    {
        date = default;
        if (!DateShape().IsMatch(value)) return false;
        var normalized = value.Replace('/', '.').Replace('-', '.');
        var formats = normalized.Split('.')[0].Length == 4
            ? new[] { "yyyy.M.d", "yyyy.MM.dd" }
            : new[] { "d.M.yyyy", "dd.MM.yyyy" };
        return DateOnly.TryParseExact(normalized, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            && date.Year is >= 2000 and <= 2100;
    }

    public static bool TryMoney(string value, out decimal money)
    {
        money = 0;
        var s = value.Trim().Replace("EUR", "", StringComparison.OrdinalIgnoreCase)
            .Replace("€", "").Replace("\u00a0", "").Replace("\u202f", "").Replace(" ", "");
        if (s.Length == 0 || s.Length > 32 || !s.All(c => char.IsDigit(c) || c is '-' or '+' or ',' or '.')) return false;
        var comma = s.LastIndexOf(','); var dot = s.LastIndexOf('.');
        var separator = comma >= 0 && dot >= 0 ? Math.Max(comma, dot) : Math.Max(comma, dot);
        if (separator >= 0)
        {
            var fractional = s.Length - separator - 1;
            if (fractional is > 0 and <= 2)
                s = s[..separator].Replace(",", "").Replace(".", "") + "." + s[(separator + 1)..];
            else s = s.Replace(",", "").Replace(".", "");
        }
        return decimal.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out money) && money >= -999_999_999m && money <= 999_999_999m;
    }
}
