using System.Globalization;
using LKvitai.MES.Modules.Finance.WebUI.IncomesReport;

namespace LKvitai.MES.Modules.Finance.WebUI.Pages;

/// <summary>What the page draws from the summary: KPI cards, remarks, chart columns, matrix, flows and labels.</summary>
public partial class Incomes
{
    internal sealed record KpiComparison(string Label, string Change, string Arrow, string Tone, string Base);
    internal sealed record KpiCard(string TestId, string Label, string Value, bool Muted, bool Danger,
        IReadOnlyList<KpiComparison> Comparisons, string? Sub, string? ActionKind, string Mini);
    internal sealed record ChartSegment(string Key, string Name, string Color, decimal Value, double Height);
    internal sealed record ChartColumn(IncomesBucketDto Bucket, string Label, string Sub, bool Weekend, bool Selected,
        bool Faded, bool Partial, IReadOnlyList<ChartSegment> Segments, string Aria, string TipTitle);
    internal sealed record ChartModel(IReadOnlyList<ChartColumn> Columns, IReadOnlyList<(string Value, double Bottom)> Ticks,
        IReadOnlyList<(string Name, string Color)> Legend, decimal WeekdayMedian);
    internal sealed record MatrixCellView(IncomesColumnDto Column, IncomesCellDto? Cell, double Heat, bool Selected);
    internal sealed record MatrixRowView(IncomesBranchDto Branch, string Color, IReadOnlyList<MatrixCellView> Cells,
        decimal Total, int Count, double Bar, decimal Share, bool Selected);
    internal sealed record FlowView(IncomesBranchDto Branch, decimal Sold, decimal Received, double SoldBar, double ReceivedBar,
        bool Selected);

    internal static string MethodColor(string code) => MethodColors.GetValueOrDefault(code, "var(--n-300)");

    /// <summary>Branch series colours by period total (unfiltered), largest first; the tail shares a neutral.</summary>
    internal Dictionary<string, string> BranchColors()
    {
        var colors = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Summary is null) return colors;
        var ranked = Summary.Branches.Where(b => b.TotalEur != 0).OrderByDescending(b => b.TotalEur).ToList();
        for (var i = 0; i < ranked.Count; i++) colors[ranked[i].Code] = i < BranchPalette.Length ? BranchPalette[i] : OtherBranchColor;
        foreach (var branch in Summary.Branches) colors.TryAdd(branch.Code, OtherBranchColor);
        return colors;
    }

    internal string BranchName(string code) => Summary?.Branches.FirstOrDefault(b => b.Code == code)?.Name ?? code;

    internal string Weekday(DateOnly day)
    {
        var names = L["Incomes.Weekdays"].Value.Split('|');
        var index = ((int)day.DayOfWeek + 6) % 7;
        return names.Length == 7 ? names[index] : day.DayOfWeek.ToString()[..2];
    }

    private string MonthName(string key, int month)
    {
        var names = L[key].Value.Split('|');
        return names.Length == 12 && month is >= 1 and <= 12 ? names[month - 1] : month.ToString(CultureInfo.InvariantCulture);
    }

    private static string Capital(string text) => text.Length == 0 ? text : char.ToUpper(text[0], CultureInfo.InvariantCulture) + text[1..];

    /// <summary>Comparison label: <c>2026-09</c> for a whole month, <c>2025</c> for a whole year, else the range.</summary>
    internal static string PeriodLabel(DateOnly from, DateOnly to)
    {
        var wholeMonths = from.Day == 1 && to.Day == DateTime.DaysInMonth(to.Year, to.Month);
        if (wholeMonths && from.Year == to.Year && from.Month == 1 && to.Month == 12) return from.Year.ToString(CultureInfo.InvariantCulture);
        if (wholeMonths && from.Year == to.Year && from.Month == to.Month) return from.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        return from.Year == to.Year
            ? $"{IncomesFormat.Date(from)}–{to:MM-dd}"
            : $"{IncomesFormat.Date(from)}–{IncomesFormat.Date(to)}";
    }

    private KpiComparison Compare(string label, decimal value, decimal baseline, Func<decimal, string> format)
    {
        if (IncomesFormat.Change(value, baseline) is not { } change) return new KpiComparison(label, "—", "", "flat", "—");
        var rounded = Math.Round(change, 1);
        var (arrow, tone) = rounded > 0 ? ("▲", "up") : rounded < 0 ? ("▼", "down") : ("■", "flat");
        return new KpiComparison(label, IncomesFormat.Signed(change), arrow, tone, format(baseline));
    }

    internal IReadOnlyList<KpiCard> Kpis()
    {
        var s = Summary;
        var t = s?.Totals ?? new IncomesTotalsDto(0, 0, new(0, 0), 0, new(0, 0));
        var empty = t.Count == 0;
        var prev = s?.Comparisons.Previous;
        var year = s?.Comparisons.LastYear;
        IReadOnlyList<KpiComparison> Cmp(Func<IncomesComparisonDto, decimal> pick, decimal value, Func<decimal, string> format) =>
            prev is null || year is null ? []
            // A year to date: the previous period is the same span last year, so one line says it all.
            : prev.From == year.From && prev.To == year.To ? [Compare(PeriodLabel(year.From, year.To), value, pick(year), format)]
            : [Compare(PeriodLabel(prev.From, prev.To), value, pick(prev), format), Compare(PeriodLabel(year.From, year.To), value, pick(year), format)];
        static decimal Avg(decimal amount, int count) => count == 0 ? 0 : amount / count;
        var average = Avg(t.AmountEur, t.Count);
        var cards = new List<KpiCard>
        {
            new("inc-kpi-total", L[IsMoney ? "Incomes.Kpi.Received" : "Incomes.Kpi.Sold"].Value, IncomesFormat.Money(t.AmountEur), empty, false,
                Cmp(c => c.AmountEur, t.AmountEur, IncomesFormat.MoneyWhole), null, null, ""),
            new("inc-kpi-count", L[IsMoney ? "Incomes.Kpi.Payments" : "Incomes.Kpi.Orders"].Value, IncomesFormat.Count(t.Count), empty, false,
                Cmp(c => c.Count, t.Count, v => IncomesFormat.Count((long)v)), null, null, ""),
            new("inc-kpi-avg", L[IsMoney ? "Incomes.Kpi.AvgPayment" : "Incomes.Kpi.AvgOrder"].Value, empty ? "—" : IncomesFormat.Money(average),
                empty, false, Cmp(c => Avg(c.AmountEur, c.Count), average, IncomesFormat.Money), null, null, ""),
        };
        if (IsMoney)
        {
            var share = t.Count == 0 ? 0 : (decimal)t.Refunds.Count / t.Count * 100;
            cards.Add(new KpiCard("inc-kpi-refunds", L["Incomes.Kpi.Refunds"].Value, IncomesFormat.Money(t.Refunds.AmountEur),
                t.Refunds.Count == 0, t.Refunds.Count > 0, [],
                L["Incomes.Kpi.RefundsSub." + IncomesFormat.PluralForm(t.Refunds.Count), IncomesFormat.Count(t.Refunds.Count),
                    IncomesFormat.Percent(share, 1)].Value,
                t.Refunds.Count > 0 ? "refunds" : null, $"{IncomesFormat.Count(t.Refunds.Count)} {Unit}"));
        }
        else
        {
            cards.Add(new KpiCard("inc-kpi-cancelled", L["Incomes.Kpi.Cancelled"].Value, IncomesFormat.Count(t.Cancelled.Count),
                t.Cancelled.Count == 0, false, [], L["Incomes.Kpi.CancelledSub", IncomesFormat.Money(t.Cancelled.AmountEur)].Value,
                t.Cancelled.Count > 0 ? "cancelled" : null, L["Incomes.Kpi.CancelledMini", IncomesFormat.MoneyWhole(t.Cancelled.AmountEur)].Value));
        }
        return cards;
    }

    internal string RemarkText(IncomesRemarkDto remark)
    {
        string V(string key) => remark.Values.TryGetValue(key, out var value) ? value : "";
        decimal D(string key) => decimal.TryParse(V(key), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0;
        int? M(string key) => int.TryParse(V(key), NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value is >= 1 and <= 12
            ? value : null;
        var count = (long)D("count");
        var form = IncomesFormat.PluralForm(count);
        return remark.Kind switch
        {
            "change" => M("month") is { } m && M("previousMonth") is { } p
                ? L["Incomes.Remark.Change", Capital(MonthName("Incomes.MonthsGenitive", m)), IncomesFormat.Signed(D("pct")),
                    MonthName("Incomes.MonthsAccusative", p)].Value
                : L["Incomes.Remark.ChangePeriod", IncomesFormat.Signed(D("pct"))].Value,
            "changeSold" => M("month") is { } m && M("previousMonth") is { } p
                ? L["Incomes.Remark.ChangeSold", Capital(MonthName("Incomes.MonthsAccusative", m)), IncomesFormat.Signed(D("pct")),
                    MonthName("Incomes.MonthsAccusative", p)].Value
                : L["Incomes.Remark.ChangeSoldPeriod", IncomesFormat.Signed(D("pct"))].Value,
            "refunds" => L["Incomes.Remark.Refunds." + form, IncomesFormat.Count(count), IncomesFormat.Money(D("amount"))].Value,
            "concentration" => L["Incomes.Remark.Concentration", V("branch"), IncomesFormat.Percent(V("share")),
                L["Incomes.MethodPhrase." + V("method")].Value, IncomesFormat.Percent(V("network"))].Value,
            "oddDay" => DateOnly.TryParseExact(V("day"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                ? L["Incomes.Remark.OddDay", V("day"), Weekday(day), IncomesFormat.MoneyWhole(D("amount")), IncomesFormat.Percent(V("pct"))].Value
                : "",
            "noEmployee" => L["Incomes.Remark.NoEmployee." + form, IncomesFormat.Count(count)].Value,
            "allEmployees" => L["Incomes.Remark.AllEmployees"].Value,
            "cancelled" => L["Incomes.Remark.Cancelled." + form, IncomesFormat.Count(count), IncomesFormat.MoneyWhole(D("amount"))].Value,
            "flowRatio" => L["Incomes.Remark.FlowRatio", IncomesFormat.Percent(V("pct"))].Value,
            "discount" => L["Incomes.Remark.Discount", IncomesFormat.Percent(V("average")), V("branch"),
                IncomesFormat.Percent(V("branchAverage"))].Value,
            _ => remark.Kind,
        };
    }

    /// <summary>Chart series: methods (money, by method) or branches with data (by period total).</summary>
    private List<(string Key, string Name, string Color)> Series()
    {
        if (Summary is null) return [];
        if (IsMoney && ChartBy == "method")
            return MethodOrder.Where(code => State.Methods is null || State.Methods.Contains(code))
                .Select(code => (code, L["Incomes.Method." + code].Value, MethodColor(code))).ToList();
        var colors = BranchColors();
        return Summary.Branches.Where(b => b.TotalEur != 0 && (State.Branches is null || State.Branches.Contains(b.Code)))
            .OrderByDescending(b => b.TotalEur)
            .Select(b => (b.Code, b.Name, colors[b.Code])).ToList();
    }

    internal ChartModel Chart()
    {
        if (Summary is null) return new ChartModel([], [], [], 0);
        var series = Series();
        var byMethod = IsMoney && ChartBy == "method";
        var bucket = Summary.Period.Bucket;
        var stacks = Summary.Buckets.Select(b => series
            .Select(x => (x.Key, x.Name, x.Color, Value: (byMethod ? b.PositiveByMethod : b.PositiveByBranch).GetValueOrDefault(x.Key)))
            .Where(x => x.Value > 0).ToList()).ToList();
        var max = Math.Max(1, stacks.Count == 0 ? 0 : stacks.Max(stack => stack.Sum(x => x.Value)));
        var step = IncomesFormat.AxisStep(max);
        var top = Math.Ceiling(max / step) * step;
        var ticks = new List<(string, double)>();
        for (var value = step; value <= top; value += step)
            ticks.Add((IncomesFormat.Number(value, 0), (double)(value / top) * PlotHeight));
        var selected = SelectedBucketKey(bucket);
        var columns = Summary.Buckets.Select((b, i) =>
        {
            var weekend = bucket == "day" && IncomesFormat.IsWeekend(b.From);
            var (label, sub, title) = bucket switch
            {
                "day" => (b.From.Day.ToString("00", CultureInfo.InvariantCulture), Weekday(b.From), $"{IncomesFormat.Date(b.From)} · {Weekday(b.From)}"),
                "week" => (L["Incomes.Week", ISOWeek.GetWeekOfYear(b.From.ToDateTime(TimeOnly.MinValue))].Value, b.From.ToString("MM-dd", CultureInfo.InvariantCulture),
                    $"{IncomesFormat.Date(b.From)} – {IncomesFormat.Date(b.To)}"),
                _ => (b.From.ToString("MM", CultureInfo.InvariantCulture), b.From.ToString("yyyy", CultureInfo.InvariantCulture), b.Key),
            };
            var partial = bucket == "month" && b.To < new DateOnly(b.From.Year, b.From.Month, DateTime.DaysInMonth(b.From.Year, b.From.Month));
            var segments = stacks[i].Select(x => new ChartSegment(x.Key, x.Name, x.Color, x.Value,
                Math.Max(1, (double)(x.Value / top) * PlotHeight))).ToList();
            var aria = $"{title}: {IncomesFormat.Money(b.AmountEur)}, {IncomesFormat.Count(b.Count)} {Unit}";
            return new ChartColumn(b, label, sub, weekend, selected == b.Key, selected is not null && selected != b.Key, partial,
                segments, aria, title);
        }).ToList();
        var weekdays = Summary.Period.Bucket == "day"
            ? Summary.Buckets.Where(b => !IncomesFormat.IsWeekend(b.From) && b.From <= Today).Select(b => b.AmountEur).Order().ToList()
            : [];
        var median = weekdays.Count == 0 ? 0
            : weekdays.Count % 2 == 1 ? weekdays[weekdays.Count / 2] : (weekdays[weekdays.Count / 2 - 1] + weekdays[weekdays.Count / 2]) / 2;
        return new ChartModel(columns, ticks, series.Select(x => (x.Name, x.Color)).ToList(), median);
    }

    private string? SelectedBucketKey(string bucket) => bucket switch
    {
        "day" => State.Day is { } day ? IncomesFormat.Date(day) : null,
        "month" => State.Month,
        _ => State.Week is { } week && Summary is not null
            ? Summary.Buckets.FirstOrDefault(b => ISOWeek.GetWeekOfYear(b.From.ToDateTime(TimeOnly.MinValue)) == week)?.Key
            : null,
    };

    internal bool ColumnSelected(string column)
    {
        var (method, week, month) = ColumnDrill(column);
        return State.DrillMethod == method && State.Week == week && State.Month == month;
    }

    internal IReadOnlyList<MatrixRowView> MatrixRows()
    {
        if (Summary is null) return [];
        var colors = BranchColors();
        var maxCell = Summary.Matrix.Count == 0 ? 1 : Math.Max(1, Summary.Matrix.Max(c => c.AmountEur));
        var total = Summary.Totals.AmountEur;
        var rows = Summary.Matrix.GroupBy(c => c.Branch)
            .Select(g => (Branch: Summary.Branches.FirstOrDefault(b => b.Code == g.Key) ?? new IncomesBranchDto(g.Key, g.Key, 0, 0, true),
                Cells: g.ToDictionary(c => c.Column, StringComparer.Ordinal), Total: g.Sum(c => c.AmountEur), Count: g.Sum(c => c.Count)))
            .OrderByDescending(x => x.Total).ToList();
        var maxRow = rows.Count == 0 ? 1 : Math.Max(1, rows.Max(x => x.Total));
        return rows.Select(x => new MatrixRowView(x.Branch, colors.GetValueOrDefault(x.Branch.Code, OtherBranchColor),
            Summary.Columns.Select(column =>
            {
                var cell = x.Cells.GetValueOrDefault(column.Key);
                var heat = cell is null ? 0 : Math.Sqrt((double)(Math.Max(0, cell.AmountEur) / maxCell));
                return new MatrixCellView(column, cell, heat, State.DrillBranch == x.Branch.Code && ColumnSelected(column.Key));
            }).ToList(),
            x.Total, x.Count, (double)(Math.Max(0, x.Total) / maxRow), total == 0 ? 0 : x.Total / total * 100,
            State.DrillBranch == x.Branch.Code)).ToList();
    }

    internal IReadOnlyList<IncomesBranchDto> BranchesWithoutData() =>
        Summary?.Branches.Where(b => (State.Branches is null || State.Branches.Contains(b.Code)) &&
                                     Summary.Matrix.All(c => c.Branch != b.Code)).ToList() ?? [];

    internal IReadOnlyList<FlowView> Flows()
    {
        if (Summary is null) return [];
        var max = Summary.SoldVsReceived.Count == 0 ? 1
            : Math.Max(1, Summary.SoldVsReceived.Max(f => Math.Max(f.SoldEur, f.ReceivedEur)));
        return Summary.SoldVsReceived.Select(f => new FlowView(
            Summary.Branches.FirstOrDefault(b => b.Code == f.Branch) ?? new IncomesBranchDto(f.Branch, f.Branch, 0, 0, true),
            f.SoldEur, f.ReceivedEur, (double)(Math.Max(0, f.SoldEur) / max), (double)(Math.Max(0, f.ReceivedEur) / max),
            State.DrillBranch == f.Branch)).ToList();
    }

    internal static string Ratio(decimal sold, decimal received) =>
        sold == 0 ? "—" : IncomesFormat.Percent(received / sold * 100);

    internal string ExportFileName =>
        $"Iplaukos_{IncomesFormat.Date(State.From)}_{IncomesFormat.Date(State.To)}_{(IsMoney ? "gauti-pinigai" : "priimti-uzsakymai")}.xlsx";

    internal string ExportSheets => L[IsMoney ? "Incomes.ExportSheetsMoney" : State.IsLong ? "Incomes.ExportSheetsOrdersMonths" : "Incomes.ExportSheetsOrdersWeeks"].Value;

    internal string MatrixTitle => L[IsMoney ? "Incomes.Matrix.Methods" : State.IsLong ? "Incomes.Matrix.Months" : "Incomes.Matrix.Weeks"].Value;

    internal string GroupLabel(string group) => L["Incomes.Group." + group switch
    {
        "day" => "Day", "week" => "Week", "month" => "Month", "branch" => "Branch", "method" => "Method",
        "employee" => "Employee", "consultant" => "Consultant", _ => "None",
    }].Value;

    internal IReadOnlyList<string> GroupOptions()
    {
        var options = new List<string> { "none", "day" };
        if (State.IsLong) options.AddRange(["week", "month"]);
        options.Add("branch");
        if (IsMoney) options.AddRange(["method", "employee"]);
        else options.Add("consultant");
        return options;
    }

    internal string FiltersText()
    {
        var branches = State.Branches is null ? L["Incomes.AllLower"].Value : string.Join(", ", State.Branches.Select(BranchName));
        var methods = !IsMoney ? L["Incomes.NotApplicable"].Value
            : State.Methods is null ? L["Incomes.AllLower"].Value
            : string.Join(", ", State.Methods.Select(code => L["Incomes.Method." + code].Value));
        var text = L["Incomes.FiltersLine", branches, methods].Value;
        if (State.Search.Length > 0) text += " · " + L["Incomes.SearchLine", State.Search].Value;
        var drill = DrillChips().Select(chip => chip.Label).ToList();
        return drill.Count == 0 ? text : text + " · " + string.Join(" · ", drill);
    }

    internal IReadOnlyList<(string Label, IncomesUrlState Without)> DrillChips()
    {
        var chips = new List<(string, IncomesUrlState)>();
        if (State.Day is { } day) chips.Add((L["Incomes.Drill.Day", IncomesFormat.Date(day), Weekday(day)].Value, State with { Day = null }));
        if (State.DrillBranch is { } branch) chips.Add((L["Incomes.Drill.Branch", BranchName(branch)].Value, State with { DrillBranch = null }));
        if (State.DrillMethod is { } method) chips.Add((L["Incomes.Drill.Method", L["Incomes.Method." + method].Value].Value, State with { DrillMethod = null }));
        if (State.Week is { } week)
        {
            var column = Summary?.Columns.FirstOrDefault(c => c.Key == "W" + week.ToString(CultureInfo.InvariantCulture));
            var range = column?.From is { } f && column.To is { } t ? $"{IncomesFormat.Date(f)} – {IncomesFormat.Date(t)}" : "";
            chips.Add((L["Incomes.Drill.Week", week, range].Value.TrimEnd(' ', ':'), State with { Week = null }));
        }
        if (State.Month is { } month) chips.Add((L["Incomes.Drill.Month", month].Value, State with { Month = null }));
        if (State.Refunds) chips.Add((L["Incomes.Drill.Refunds"].Value, State with { Refunds = false }));
        if (State.NoEmployee) chips.Add((L["Incomes.Drill.NoEmployee"].Value, State with { NoEmployee = false }));
        if (State.Cancelled) chips.Add((L["Incomes.Drill.Cancelled"].Value, State with { Cancelled = false }));
        return chips;
    }

    internal string PresetLabel(string preset) => L["Incomes.Preset" + preset switch
    {
        "this" => "ThisMonth", "last" => "LastMonth", "q" => "ThisQuarter", "y" => "ThisYear", _ => "Custom",
    }].Value;

    internal string CurrentPreset => IncomesPresets.Detect(State.From, State.To, Today);

    internal static string Heat(double t, int top = 33) =>
        string.Create(CultureInfo.InvariantCulture, $"--t:{t:0.###};--hi:{top}%");

    internal static string Pct(double fraction) => string.Create(CultureInfo.InvariantCulture, $"{Math.Clamp(fraction, 0, 1) * 100:0.##}%");

    internal static string Px(double value) => string.Create(CultureInfo.InvariantCulture, $"{value:0.#}px");

    internal static string Frac(double value) => Math.Clamp(value, 0, 1).ToString("0.###", CultureInfo.InvariantCulture);

    internal string CellAria(MatrixRowView row, MatrixCellView cell) =>
        $"{row.Branch.Name}, {ColumnLabel(cell.Column)}: {(cell.Cell is null ? L["Incomes.None"].Value : IncomesFormat.Money(cell.Cell.AmountEur))}";

    internal string DocumentText(IncomesRowDto row) => row.Method switch
    {
        "BANK" when row.BankTransferNo is { } nr => "NR " + nr.ToString(CultureInfo.InvariantCulture),
        "PPK" when row.PpkNo is { } ppk => "PPK " + ppk,
        "EKA" when row.EkaReceiptNo is { } eka => "EKA-" + eka.ToString(CultureInfo.InvariantCulture),
        _ => "—",
    };

    internal static string OrderHref(string orderNo) => "/sales/orders/" + Uri.EscapeDataString(orderNo);

    internal static string BankHref(int nr) => "bank-payments?nr=" + nr.ToString(CultureInfo.InvariantCulture);
}
