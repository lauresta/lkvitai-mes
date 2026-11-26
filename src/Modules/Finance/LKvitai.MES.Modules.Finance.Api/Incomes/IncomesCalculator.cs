using System.Globalization;

namespace LKvitai.MES.Modules.Finance.Api.Incomes;

/// <summary>Rows of the legacy database the report is computed from (each list already limited to its period).</summary>
public sealed record IncomesSources(
    IReadOnlyList<IncomesBranch> Branches,
    IReadOnlyList<IncomesRecord> Current,
    IReadOnlyList<IncomesRecord> Previous,
    IReadOnlyList<IncomesRecord> LastYear,
    IReadOnlyList<IncomesRecord> Payments,
    IReadOnlyList<IncomesRecord> Orders);

/// <summary>Summary of the report: KPIs, comparisons, chart buckets, matrix, flows and remarks.</summary>
public static class IncomesCalculator
{
    private static readonly CultureInfo Lithuanian = CultureInfo.GetCultureInfo("lt-LT");
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    public const int MaxRemarks = 5;

    public static IncomesSummary Summary(IncomesQuery query, IncomesSources sources, DateOnly today)
    {
        var branches = sources.Branches.ToDictionary(branch => branch.Id);
        var current = InPeriod(sources.Current, query.From, query.To, branches);
        var filtered = current.Where(record => BranchOk(query, branches[record.BranchId]) && MethodOk(query, record)).ToList();

        var branchInfos = sources.Branches
            .Select(branch =>
            {
                var count = current.Count(record => record.BranchId == branch.Id && MethodOk(query, record));
                var total = current.Where(record => record.BranchId == branch.Id).Sum(record => record.Amount);
                return new IncomesBranchInfo(branch.Code, branch.Name, count, total, count > 0);
            })
            .OrderBy(branch => branch.Name, StringComparer.Create(Lithuanian, ignoreCase: true))
            .ToList();
        var methods = IncomesMethods.All
            .Select(code => new IncomesMethodInfo(code, IncomesMethods.Name(code), IncomesMethods.Hint(code),
                query.IsMoney ? current.Count(record => record.Method == code && BranchOk(query, branches[record.BranchId])) : 0))
            .ToList();

        var totals = Totals(filtered);
        var (previousFrom, previousTo) = IncomesPeriod.Previous(query.From, query.To);
        var (lastYearFrom, lastYearTo) = IncomesPeriod.LastYear(query.From, query.To);
        var comparisons = new IncomesComparisons(
            Compare(query, sources.Previous, previousFrom, previousTo, branches),
            Compare(query, sources.LastYear, lastYearFrom, lastYearTo, branches));

        var (columns, matrix) = Matrix(query, filtered, branches);
        var flows = SoldVsReceived(query, sources, branches);
        var remarks = Remarks(query, filtered, totals, comparisons.Previous, flows, branches, today);
        var period = new IncomesPeriodInfo(query.From, query.To, query.Bucket, query.Days,
            IncomesPeriod.Workdays(query.From, query.To));
        return new IncomesSummary(period, query.IsMoney ? "money" : "orders", branchInfos, methods, totals, comparisons,
            Buckets(query, filtered, branches), columns, matrix, flows, remarks, today);
    }

    private static List<IncomesRecord> InPeriod(IEnumerable<IncomesRecord> records, DateOnly from, DateOnly to,
        Dictionary<int, IncomesBranch> branches) =>
        records.Where(record => record.Date >= from && record.Date <= to && branches.ContainsKey(record.BranchId)).ToList();

    internal static bool BranchOk(IncomesQuery query, IncomesBranch branch) =>
        query.Branches is null || query.Branches.Contains(branch.Code);

    internal static bool MethodOk(IncomesQuery query, IncomesRecord record) =>
        !query.IsMoney || query.Methods is null || (record.Method is not null && query.Methods.Contains(record.Method));

    private static IncomesTotals Totals(IReadOnlyCollection<IncomesRecord> records)
    {
        var refunds = records.Where(record => record.Amount < 0).ToList();
        var cancelled = records.Where(record => record.Cancelled).ToList();
        return new IncomesTotals(records.Count, records.Sum(record => record.Amount),
            new IncomesCountSum(refunds.Count, refunds.Sum(record => record.Amount)),
            records.Count(record => record.Method is not null && string.IsNullOrWhiteSpace(record.Person)),
            new IncomesCountSum(cancelled.Count, cancelled.Sum(record => record.Amount)));
    }

    private static IncomesComparison Compare(IncomesQuery query, IEnumerable<IncomesRecord> records, DateOnly from, DateOnly to,
        Dictionary<int, IncomesBranch> branches)
    {
        var rows = InPeriod(records, from, to, branches)
            .Where(record => BranchOk(query, branches[record.BranchId]) && MethodOk(query, record))
            .ToList();
        var refunds = rows.Where(record => record.Amount < 0).ToList();
        return new IncomesComparison(from, to, rows.Count, rows.Sum(record => record.Amount), refunds.Count,
            refunds.Sum(record => record.Amount));
    }

    private static List<IncomesBucket> Buckets(IncomesQuery query, IReadOnlyCollection<IncomesRecord> records,
        Dictionary<int, IncomesBranch> branches)
    {
        var byKey = records.ToLookup(record => IncomesPeriod.BucketKey(record.Date, query.Bucket));
        return IncomesPeriod.Buckets(query.From, query.To, query.Bucket).Select(bucket =>
        {
            var rows = byKey[bucket.Key].ToList();
            var positive = rows.Where(record => record.Amount > 0).ToList();
            var byMethod = query.IsMoney
                ? positive.GroupBy(record => record.Method ?? "").ToDictionary(g => g.Key, g => g.Sum(record => record.Amount))
                : new Dictionary<string, decimal>();
            var byBranch = positive.GroupBy(record => branches[record.BranchId].Code)
                .ToDictionary(g => g.Key, g => g.Sum(record => record.Amount));
            return new IncomesBucket(bucket.Key, bucket.From, bucket.To, rows.Count, rows.Sum(record => record.Amount),
                byMethod, byBranch);
        }).ToList();
    }

    /// <summary>Matrix column key of a record: its method (money), ISO week (orders, short period) or month (orders, long period).</summary>
    public static string ColumnKey(IncomesQuery query, IncomesRecord record) =>
        query.IsMoney ? record.Method ?? ""
        : query.Days > IncomesPeriod.LongPeriodDays ? "M" + IncomesPeriod.MonthKey(record.Date)
        : "W" + IncomesPeriod.Week(record.Date).ToString(Invariant);

    private static (List<IncomesColumn> Columns, List<IncomesMatrixCell> Cells) Matrix(IncomesQuery query,
        IReadOnlyCollection<IncomesRecord> records, Dictionary<int, IncomesBranch> branches)
    {
        List<IncomesColumn> columns;
        if (query.IsMoney)
            columns = IncomesMethods.All.Where(code => query.Methods is null || query.Methods.Contains(code))
                .Select(code => new IncomesColumn(code, IncomesMethods.Name(code), null, null, null)).ToList();
        else if (query.Days > IncomesPeriod.LongPeriodDays)
            columns = IncomesPeriod.Buckets(query.From, query.To, "month")
                .Select(month => new IncomesColumn("M" + month.Key, month.Key, null, month.From, month.To)).ToList();
        else
            columns = IncomesPeriod.Buckets(query.From, query.To, "week")
                .Select(week =>
                {
                    var number = IncomesPeriod.Week(week.From).ToString(Invariant);
                    var label = week.From.Month == week.To.Month
                        ? $"{week.From:MM-dd}–{week.To:dd}" : $"{week.From:MM-dd}–{week.To:MM-dd}";
                    return new IncomesColumn("W" + number, label, number, week.From, week.To);
                }).ToList();
        var cells = records
            .GroupBy(record => (Branch: branches[record.BranchId].Code, Column: ColumnKey(query, record)))
            .Select(g => new IncomesMatrixCell(g.Key.Branch, g.Key.Column, g.Count(), g.Sum(record => record.Amount)))
            .OrderBy(cell => cell.Branch, StringComparer.Ordinal).ThenBy(cell => cell.Column, StringComparer.Ordinal)
            .ToList();
        return (columns, cells);
    }

    private static List<IncomesSoldVsReceived> SoldVsReceived(IncomesQuery query, IncomesSources sources,
        Dictionary<int, IncomesBranch> branches)
    {
        var orders = InPeriod(sources.Orders, query.From, query.To, branches);
        var payments = InPeriod(sources.Payments, query.From, query.To, branches);
        return sources.Branches.Where(branch => BranchOk(query, branch))
            .Select(branch => new IncomesSoldVsReceived(branch.Code,
                orders.Where(record => record.BranchId == branch.Id).Sum(record => record.Amount),
                payments.Where(record => record.BranchId == branch.Id).Sum(record => record.Amount)))
            .Where(flow => flow.SoldEur != 0 || flow.ReceivedEur != 0)
            .OrderByDescending(flow => flow.ReceivedEur).ThenByDescending(flow => flow.SoldEur)
            .ToList();
    }

    private static List<IncomesRemark> Remarks(IncomesQuery query, IReadOnlyCollection<IncomesRecord> records,
        IncomesTotals totals, IncomesComparison previous, IReadOnlyCollection<IncomesSoldVsReceived> flows,
        Dictionary<int, IncomesBranch> branches, DateOnly today)
    {
        var remarks = new List<IncomesRemark>();
        if (Change(query, totals, previous) is { } change) remarks.Add(change);
        if (query.IsMoney)
        {
            if (totals.Refunds.Count > 0)
                remarks.Add(new IncomesRemark("refunds", "danger", "↺", Values(("count", Count(totals.Refunds.Count)),
                    ("amount", Money(totals.Refunds.AmountEur))), new IncomesDrill(Refunds: true)));
            if (Concentration(records, branches) is { } concentration) remarks.Add(concentration);
        }
        else
        {
            if (totals.Cancelled.Count > 0)
                remarks.Add(new IncomesRemark("cancelled", "warn", "×", Values(("count", Count(totals.Cancelled.Count)),
                    ("amount", Money(totals.Cancelled.AmountEur))), new IncomesDrill(Cancelled: true)));
            if (totals.AmountEur > 0)
                remarks.Add(new IncomesRemark("flowRatio", "info", "⇄", Values(("pct",
                    Percent(flows.Sum(flow => flow.ReceivedEur) / totals.AmountEur * 100, 0))), null));
            if (Discount(records, branches) is { } discount) remarks.Add(discount);
        }
        if (OddDay(query, records, today) is { } oddDay) remarks.Add(oddDay);
        if (query.IsMoney)
            remarks.Add(totals.NoEmployeeCount > 0
                ? new IncomesRemark("noEmployee", "warn", "?", Values(("count", Count(totals.NoEmployeeCount))),
                    new IncomesDrill(NoEmployee: true))
                : new IncomesRemark("allEmployees", "ok", "✓", Values(), null));
        return remarks.Take(MaxRemarks).ToList();
    }

    private static IncomesRemark? Change(IncomesQuery query, IncomesTotals totals, IncomesComparison previous)
    {
        if (previous.AmountEur == 0) return null;
        var pct = (totals.AmountEur - previous.AmountEur) / Math.Abs(previous.AmountEur) * 100;
        var wholeMonth = query.From.Day == 1 && IncomesPeriod.IsLastDayOfMonth(query.To) &&
                         query.From.Month == query.To.Month && query.From.Year == query.To.Year;
        var values = Values(("pct", Percent(pct, 1)), ("month", wholeMonth ? query.From.Month.ToString(Invariant) : ""),
            ("previousMonth", wholeMonth ? previous.From.Month.ToString(Invariant) : ""));
        return new IncomesRemark(query.IsMoney ? "change" : "changeSold", pct < 0 ? "danger" : "ok", pct < 0 ? "▼" : "▲",
            values, null);
    }

    /// <summary>A branch with at least 30 rows whose share of one method beats the network share by more than 15 pp.</summary>
    private static IncomesRemark? Concentration(IReadOnlyCollection<IncomesRecord> records, Dictionary<int, IncomesBranch> branches)
    {
        if (records.Count == 0) return null;
        var network = records.GroupBy(record => record.Method ?? "").ToDictionary(g => g.Key, g => (decimal)g.Count() / records.Count);
        var best = records.GroupBy(record => record.BranchId)
            .Where(g => g.Count() >= 30)
            .SelectMany(g => g.GroupBy(record => record.Method ?? "").Select(m =>
                (Branch: branches[g.Key], Method: m.Key, Share: (decimal)m.Count() / g.Count())))
            .Select(x => (x.Branch, x.Method, x.Share, Network: network[x.Method], Gap: x.Share - network[x.Method]))
            .Where(x => x.Gap > 0.15m && x.Method.Length > 0)
            .OrderByDescending(x => x.Gap).ThenBy(x => x.Branch.Name, StringComparer.Ordinal)
            .FirstOrDefault();
        return best.Branch is null ? null
            : new IncomesRemark("concentration", "info", "%", Values(("branch", best.Branch.Name), ("method", best.Method),
                ("share", Percent(best.Share * 100, 0)), ("network", Percent(best.Network * 100, 0))),
                new IncomesDrill(Branch: best.Branch.Code, Method: best.Method));
    }

    /// <summary>Order-value weighted average discount, and the branch (at least 30 orders) with the highest one.</summary>
    private static IncomesRemark? Discount(IReadOnlyCollection<IncomesRecord> records, Dictionary<int, IncomesBranch> branches)
    {
        static decimal Weighted(IEnumerable<IncomesRecord> rows)
        {
            var list = rows.Where(record => record.Amount > 0).ToList();
            var value = list.Sum(record => record.Amount);
            return value == 0 ? 0 : list.Sum(record => (record.DiscountPct ?? 0) * record.Amount) / value;
        }
        var top = records.GroupBy(record => record.BranchId).Where(g => g.Count() >= 30)
            .Select(g => (Branch: branches[g.Key], Average: Weighted(g)))
            .OrderByDescending(x => x.Average).ThenBy(x => x.Branch.Name, StringComparer.Ordinal)
            .FirstOrDefault();
        return top.Branch is null ? null
            : new IncomesRemark("discount", "info", "%", Values(("average", Percent(Weighted(records), 1)),
                ("branch", top.Branch.Name), ("branchAverage", Percent(top.Average, 1))), new IncomesDrill(Branch: top.Branch.Code));
    }

    /// <summary>The weekday (up to today) furthest below 60 % of the weekday median.</summary>
    private static IncomesRemark? OddDay(IncomesQuery query, IReadOnlyCollection<IncomesRecord> records, DateOnly today)
    {
        var byDay = records.GroupBy(record => record.Date).ToDictionary(g => g.Key, g => g.Sum(record => record.Amount));
        var weekdays = new List<(DateOnly Day, decimal Amount)>();
        for (var day = query.From; day <= query.To && day <= today; day = day.AddDays(1))
            if (!IncomesPeriod.IsWeekend(day)) weekdays.Add((day, byDay.GetValueOrDefault(day)));
        if (weekdays.Count < 3) return null;
        var median = Median(weekdays.Select(x => x.Amount).ToList());
        if (median <= 0) return null;
        var odd = weekdays.Where(x => x.Amount < median * 0.6m).OrderBy(x => x.Amount).ThenBy(x => x.Day).FirstOrDefault();
        return odd.Day == default ? null
            : new IncomesRemark("oddDay", "warn", "!", Values(("day", odd.Day.ToString("yyyy-MM-dd", Invariant)),
                ("amount", Money(odd.Amount)), ("pct", Percent((1 - odd.Amount / median) * 100, 0)),
                ("median", Money(median))), new IncomesDrill(Day: odd.Day));
    }

    public static decimal Median(IReadOnlyList<decimal> values)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(value => value).ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }

    private static Dictionary<string, string> Values(params (string Key, string Value)[] values) =>
        values.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

    private static string Count(int value) => value.ToString(Invariant);
    private static string Money(decimal value) => Math.Round(value, 2).ToString("0.00", Invariant);
    private static string Percent(decimal value, int digits) => Math.Round(value, digits, MidpointRounding.AwayFromZero)
        .ToString(digits == 0 ? "0" : "0.0", Invariant);
}
