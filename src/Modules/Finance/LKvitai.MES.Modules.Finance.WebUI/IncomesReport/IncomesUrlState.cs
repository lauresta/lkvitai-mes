using System.Globalization;
using System.Text;

namespace LKvitai.MES.Modules.Finance.WebUI.IncomesReport;

/// <summary>
/// Everything the page shows, as the shareable URL:
/// <c>/finance/incomes?from=2026-10-01&amp;to=2026-10-31&amp;ds=orders&amp;branch=VLN,KNS&amp;method=BANK,EKA&amp;group=branch&amp;sort=-amt&amp;day=2026-10-14&amp;dBranch=KNS&amp;dMethod=EKA&amp;dWeek=41&amp;refunds=1&amp;noEmployee=1&amp;cancelled=1&amp;q=…</c>.
/// The period is always written (presets are relative to today); other defaults are omitted. The API reads the same names.
/// </summary>
public sealed record IncomesUrlState
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public bool Orders { get; init; }
    /// <summary>Sorted branch codes; null = all.</summary>
    public IReadOnlyList<string>? Branches { get; init; }
    /// <summary>Sorted method codes; null = all.</summary>
    public IReadOnlyList<string>? Methods { get; init; }
    /// <summary>Table grouping; null = the default for the period (<see cref="DefaultGroup"/>).</summary>
    public string? Group { get; init; }
    public string Sort { get; init; } = "date";
    public bool SortDesc { get; init; } = true;
    public DateOnly? Day { get; init; }
    public int? Week { get; init; }
    public string? Month { get; init; }
    public string? DrillBranch { get; init; }
    public string? DrillMethod { get; init; }
    public bool Refunds { get; init; }
    public bool NoEmployee { get; init; }
    public bool Cancelled { get; init; }
    public string Search { get; init; } = "";

    public const int LongPeriodDays = 62;
    public int Days => To.DayNumber - From.DayNumber + 1;
    public bool IsLong => Days > LongPeriodDays;
    public string DefaultGroup => IsLong ? "month" : "none";
    public string EffectiveGroup => Group ?? DefaultGroup;
    public bool HasDrill => Day is not null || Week is not null || Month is not null || DrillBranch is not null ||
                            DrillMethod is not null || Refunds || NoEmployee || Cancelled;

    public static IncomesUrlState Default(DateOnly today)
    {
        var (from, to) = IncomesPresets.Range("last", today);
        return new IncomesUrlState { From = from, To = to };
    }

    /// <summary>Header filters changed: drill filters go, search stays.</summary>
    public IncomesUrlState WithoutDrill() => this with
    {
        Day = null, Week = null, Month = null, DrillBranch = null, DrillMethod = null,
        Refunds = false, NoEmployee = false, Cancelled = false,
    };

    public static IncomesUrlState Parse(IReadOnlyDictionary<string, string?> query, DateOnly today)
    {
        string? Get(string key) => query.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
        var state = Default(today);
        var from = ParseDate(Get("from"));
        var to = ParseDate(Get("to"));
        if (from is not null && to is not null && to >= from && to.Value.DayNumber - from.Value.DayNumber < 1100)
            state = state with { From = from.Value, To = to.Value };
        var orders = Get("ds") == "orders";
        var sort = Get("sort") ?? "-date";
        return state with
        {
            Orders = orders,
            Branches = Codes(Get("branch")),
            Methods = orders ? null : Codes(Get("method")),
            Group = Get("group"),
            Sort = sort.TrimStart('-'),
            SortDesc = sort.StartsWith('-'),
            Day = ParseDate(Get("day")),
            Week = int.TryParse(Get("dWeek"), NumberStyles.None, CultureInfo.InvariantCulture, out var week) && week is >= 1 and <= 53
                ? week : null,
            Month = Get("dMonth") is { Length: 7 } month && DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ? month : null,
            DrillBranch = Get("dBranch")?.ToUpperInvariant(),
            DrillMethod = orders ? null : Get("dMethod")?.ToUpperInvariant(),
            Refunds = !orders && Get("refunds") == "1",
            NoEmployee = !orders && Get("noEmployee") == "1",
            Cancelled = orders && Get("cancelled") == "1",
            Search = Get("q") is { } q && q.Length <= 100 ? q : "",
        };
    }

    /// <summary>The query string (without "?"), defaults omitted except the period.</summary>
    public string ToQuery()
    {
        var parts = new List<string> { "from=" + IncomesFormat.Date(From), "to=" + IncomesFormat.Date(To) };
        void Add(string key, string? value) { if (!string.IsNullOrEmpty(value)) parts.Add(key + "=" + Uri.EscapeDataString(value)); }
        void AddList(string key, IReadOnlyList<string>? codes)
        {
            if (codes is { Count: > 0 }) parts.Add(key + "=" + string.Join(',', codes.Select(Uri.EscapeDataString)));
        }
        Add("ds", Orders ? "orders" : null);
        AddList("branch", Branches);
        AddList("method", Methods);
        Add("group", Group);
        Add("sort", Sort == "date" && SortDesc ? null : (SortDesc ? "-" : "") + Sort);
        Add("day", Day is { } day ? IncomesFormat.Date(day) : null);
        Add("dBranch", DrillBranch);
        Add("dMethod", DrillMethod);
        Add("dWeek", Week?.ToString(CultureInfo.InvariantCulture));
        Add("dMonth", Month);
        Add("refunds", Refunds ? "1" : null);
        Add("noEmployee", NoEmployee ? "1" : null);
        Add("cancelled", Cancelled ? "1" : null);
        Add("q", Search.Length == 0 ? null : Search);
        return string.Join('&', parts);
    }

    /// <summary>Toggles <paramref name="code"/> in a multi-choice filter: all selected collapses back to "all" (null).</summary>
    public static IReadOnlyList<string>? Toggle(IReadOnlyList<string>? selected, string code, IReadOnlyCollection<string> all)
    {
        var set = new SortedSet<string>(selected ?? [], StringComparer.Ordinal);
        if (selected is null) set = new SortedSet<string>([code], StringComparer.Ordinal);
        else if (!set.Remove(code)) set.Add(code);
        return set.Count == 0 || (all.Count > 0 && all.All(set.Contains)) ? null : set.ToList();
    }

    private static DateOnly? ParseDate(string? text) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null;

    private static IReadOnlyList<string>? Codes(string? text)
    {
        if (text is null) return null;
        var codes = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(code => code.Length <= 10)
            .Select(code => code.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        return codes.Count == 0 ? null : codes;
    }

    /// <summary>Parses the query part of an absolute or relative URI into a dictionary (last value wins).</summary>
    public static IReadOnlyDictionary<string, string?> QueryOf(string uri)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var start = uri.IndexOf('?');
        if (start < 0) return result;
        var end = uri.IndexOf('#', start);
        var query = end < 0 ? uri[(start + 1)..] : uri[(start + 1)..end];
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            var key = Uri.UnescapeDataString((eq < 0 ? pair : pair[..eq]).Replace('+', ' '));
            var value = eq < 0 ? "" : Uri.UnescapeDataString(pair[(eq + 1)..].Replace('+', ' '));
            result[key] = value;
        }
        return result;
    }
}

/// <summary>Period presets of the filter panel, derived from the period itself (no separate URL parameter).</summary>
public static class IncomesPresets
{
    public static IReadOnlyList<string> All { get; } = ["this", "last", "q", "y", "custom"];

    public static (DateOnly From, DateOnly To) Range(string preset, DateOnly today)
    {
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        return preset switch
        {
            "this" => (monthStart, today),
            "q" => (new DateOnly(today.Year, (today.Month - 1) / 3 * 3 + 1, 1), today),
            "y" => (new DateOnly(today.Year, 1, 1), today),
            _ => (monthStart.AddMonths(-1), monthStart.AddDays(-1)),
        };
    }

    public static string Detect(DateOnly from, DateOnly to, DateOnly today)
    {
        foreach (var preset in new[] { "last", "this", "q", "y" })
            if (Range(preset, today) == (from, to)) return preset;
        return "custom";
    }

    /// <summary>The whole month before (−1) or after (+1) the month the period starts in; never past today.</summary>
    public static (DateOnly From, DateOnly To)? StepMonth(DateOnly from, int direction, DateOnly today)
    {
        var start = new DateOnly(from.Year, from.Month, 1).AddMonths(direction);
        if (start > today) return null;
        var end = start.AddMonths(1).AddDays(-1);
        return (start, end > today ? today : end);
    }
}

/// <summary>A small string builder helper for space-separated CSS class lists.</summary>
internal static class Css
{
    public static string Join(params (string Name, bool On)[] classes)
    {
        var builder = new StringBuilder();
        foreach (var (name, on) in classes)
        {
            if (!on) continue;
            if (builder.Length > 0) builder.Append(' ');
            builder.Append(name);
        }
        return builder.ToString();
    }
}
