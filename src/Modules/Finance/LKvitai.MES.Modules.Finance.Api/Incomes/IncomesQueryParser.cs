using System.Globalization;
using Microsoft.Extensions.Primitives;

namespace LKvitai.MES.Modules.Finance.Api.Incomes;

/// <summary>Reads and validates the incomes query string (the same names the page puts into its URL).</summary>
public static class IncomesQueryParser
{
    public const int MaxTake = 60_000;
    private static readonly string[] MoneyGroups = ["none", "day", "week", "month", "branch", "method", "employee"];
    private static readonly string[] OrderGroups = ["none", "day", "week", "month", "branch", "consultant"];
    private static readonly string[] MoneySorts = ["date", "order", "branch", "method", "emp", "amt"];
    private static readonly string[] OrderSorts = ["date", "order", "branch", "emp", "cust", "disc", "paid", "status", "amt"];

    public static IncomesQuery Parse(IReadOnlyDictionary<string, StringValues> query, DateOnly today)
    {
        string? Get(string key) => query.TryGetValue(key, out var value) && !StringValues.IsNullOrEmpty(value)
            ? value.ToString().Trim() : null;

        var (defaultFrom, defaultTo) = IncomesPeriod.LastMonth(today);
        var from = Date(Get("from"), "from") ?? defaultFrom;
        var to = Date(Get("to"), "to") ?? defaultTo;
        if (to < from) throw new IncomesQueryException("'to' precedes 'from'.");
        if (to.DayNumber - from.DayNumber + 1 > IncomesPeriod.MaxDays)
            throw new IncomesQueryException($"The period can be at most {IncomesPeriod.MaxDays} days.");
        if (from.Year < 2000 || to.Year > 2100) throw new IncomesQueryException("Invalid period.");

        var dataset = Get("ds") switch
        {
            null or "money" => IncomesDataset.Money,
            "orders" => IncomesDataset.Orders,
            _ => throw new IncomesQueryException("ds must be money or orders."),
        };
        var money = dataset == IncomesDataset.Money;

        var methods = Codes(Get("method"));
        if (methods?.Any(code => !IncomesMethods.All.Contains(code)) == true)
            throw new IncomesQueryException("Unknown method.");
        var drillMethod = Get("dMethod")?.ToUpperInvariant();
        if (drillMethod is not null && !IncomesMethods.All.Contains(drillMethod))
            throw new IncomesQueryException("Unknown method.");

        var bucket = Get("bucket") ?? IncomesPeriod.DefaultBucket(from, to);
        if (bucket is not ("day" or "week" or "month")) throw new IncomesQueryException("bucket must be day, week or month.");
        if (bucket == "day" && to.DayNumber - from.DayNumber + 1 > IncomesPeriod.MaxDailyBucketDays) bucket = "month";

        var group = Get("group") ?? (to.DayNumber - from.DayNumber + 1 > IncomesPeriod.LongPeriodDays ? "month" : "none");
        if (!(money ? MoneyGroups : OrderGroups).Contains(group)) group = "none";

        var sortText = Get("sort") ?? "-date";
        var desc = sortText.StartsWith('-');
        var sort = sortText.TrimStart('-');
        if (!(money ? MoneySorts : OrderSorts).Contains(sort)) { sort = "date"; desc = true; }

        var search = Get("q") ?? "";
        if (search.Length > 100) throw new IncomesQueryException("Search is too long.");

        var week = Get("dWeek") is { } weekText
            ? int.TryParse(weekText, NumberStyles.None, CultureInfo.InvariantCulture, out var w) && w is >= 1 and <= 53
                ? w : throw new IncomesQueryException("Invalid week.")
            : (int?)null;
        var month = Get("dMonth");
        if (month is not null && !DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _))
            throw new IncomesQueryException("Invalid month.");

        var skip = Number(Get("skip"), 0, int.MaxValue, 0);
        var take = Number(Get("take"), 1, MaxTake, 100);

        return new IncomesQuery
        {
            From = from,
            To = to,
            Dataset = dataset,
            Branches = Codes(Get("branch")),
            Methods = money ? methods : null,
            Bucket = bucket,
            Day = Date(Get("day"), "day"),
            Week = week,
            Month = month,
            DrillBranch = Get("dBranch")?.ToUpperInvariant(),
            DrillMethod = money ? drillMethod : null,
            Refunds = money && Flag(Get("refunds")),
            NoEmployee = money && Flag(Get("noEmployee")),
            CancelledOnly = !money && Flag(Get("cancelled")),
            Search = search,
            Sort = sort,
            SortDesc = desc,
            Group = group,
            Skip = skip,
            Take = take,
        };
    }

    private static DateOnly? Date(string? text, string name) =>
        text is null ? null
        : DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value : throw new IncomesQueryException($"'{name}' must be yyyy-MM-dd.");

    private static HashSet<string>? Codes(string? text)
    {
        if (text is null) return null;
        var codes = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(code => code.ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);
        if (codes.Count > 50 || codes.Any(code => code.Length > 10)) throw new IncomesQueryException("Too many codes.");
        return codes.Count == 0 ? null : codes;
    }

    private static bool Flag(string? text) => text is "1" or "true";

    private static int Number(string? text, int min, int max, int fallback) =>
        text is null ? fallback
        : int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= min && value <= max
            ? value : throw new IncomesQueryException("Invalid paging.");
}
