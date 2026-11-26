using System.Globalization;

namespace LKvitai.MES.Modules.Finance.Api.Incomes;

/// <summary>A table row with its group, in the order the table (and its export) shows it.</summary>
public sealed record IncomesTableRow(IncomesRecord Record, IncomesBranch Branch, string GroupKey);

/// <summary>The detail table: header filters, drill filters and search, sorted and grouped.</summary>
public sealed record IncomesTable(IReadOnlyList<IncomesTableRow> Rows, IReadOnlyList<IncomesGroup> Groups)
{
    // Invariant, not lt-LT: Lithuanian collation treats š, ž, č as letters of their own, so "siauliai" would not find "Šiauliai".
    private static readonly CompareInfo Compare = CultureInfo.InvariantCulture.CompareInfo;
    private static readonly StringComparer Names = StringComparer.Create(CultureInfo.GetCultureInfo("lt-LT"), ignoreCase: true);
    public const string NoPersonKey = "";

    public decimal Sum => Rows.Sum(row => row.Record.Amount);
    public decimal Refunds => Rows.Where(row => row.Record.Amount < 0).Sum(row => row.Record.Amount);

    public static IncomesTable Build(IncomesQuery query, IReadOnlyList<IncomesBranch> branchList, IEnumerable<IncomesRecord> records)
    {
        var branches = branchList.ToDictionary(branch => branch.Id);
        var rows = records
            .Where(record => record.Date >= query.From && record.Date <= query.To && branches.ContainsKey(record.BranchId))
            .Select(record => new IncomesTableRow(record, branches[record.BranchId], GroupKey(query, record, branches[record.BranchId])))
            .Where(row => IncomesCalculator.BranchOk(query, row.Branch) && IncomesCalculator.MethodOk(query, row.Record) &&
                          DrillOk(query, row) && SearchOk(query, row.Record))
            .ToList();

        var comparison = Comparison(query);
        rows.Sort(comparison);
        if (query.Group == "none") return new IncomesTable(rows, []);

        var groups = rows.GroupBy(row => row.GroupKey)
            .Select(g => new IncomesGroup(g.Key, GroupLabel(query, g.First()), g.Count(), g.Sum(row => row.Record.Amount)))
            .ToList();
        groups = OrderGroups(query, groups);
        var order = groups.Select((group, index) => (group.Key, index)).ToDictionary(x => x.Key, x => x.index, StringComparer.Ordinal);
        rows = rows.OrderBy(row => order[row.GroupKey]).ToList(); // OrderBy is stable: the sort inside a group stays
        return new IncomesTable(rows, groups);
    }

    public IncomesRowsPage Page(int skip, int take) =>
        new(Rows.Count, Sum, Refunds, Groups, Rows.Skip(skip).Take(take).Select(ToRow).ToList());

    public static IncomesRow ToRow(IncomesTableRow row)
    {
        var r = row.Record;
        return r.Method is not null
            ? new IncomesRow(r.Id, r.Date, r.EnteredAt, r.OrderNo, row.Branch.Name, row.Branch.Code, r.Series,
                Method: r.Method, AmountEur: r.Amount, Employee: r.Person, Note: r.Note, PpkNo: r.PpkNo,
                BankTransferNo: r.BankTransferNo, EkaReceiptNo: r.EkaReceiptNo)
            : new IncomesRow(r.Id, r.Date, r.EnteredAt, r.OrderNo, row.Branch.Name, row.Branch.Code, r.Series,
                Note: r.Note, Consultant: r.Person, Customer: r.Customer, ValueEur: r.Amount, DiscountPct: r.DiscountPct,
                PaidEur: r.PaidEur, Status: r.Cancelled ? "cancelled" : "accepted");
    }

    private static bool DrillOk(IncomesQuery query, IncomesTableRow row)
    {
        var r = row.Record;
        return (query.Day is null || r.Date == query.Day) &&
               (query.Week is null || IncomesPeriod.Week(r.Date) == query.Week) &&
               (query.Month is null || IncomesPeriod.MonthKey(r.Date) == query.Month) &&
               (query.DrillBranch is null || row.Branch.Code == query.DrillBranch) &&
               (query.DrillMethod is null || r.Method == query.DrillMethod) &&
               (!query.Refunds || r.Amount < 0) &&
               (!query.NoEmployee || string.IsNullOrWhiteSpace(r.Person)) &&
               (!query.CancelledOnly || r.Cancelled);
    }

    /// <summary>Case- and accent-insensitive: "siauliai" finds "Šiauliai".</summary>
    private static bool SearchOk(IncomesQuery query, IncomesRecord r)
    {
        if (query.Search.Length == 0) return true;
        string?[] fields = [r.OrderNo, r.Note, r.Person, r.Customer, r.PpkNo,
            r.BankTransferNo?.ToString(CultureInfo.InvariantCulture), r.EkaReceiptNo?.ToString(CultureInfo.InvariantCulture)];
        return fields.Any(field => field is not null &&
            Compare.IndexOf(field, query.Search, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);
    }

    private static int ByDate(IncomesTableRow a, IncomesTableRow b)
    {
        var result = a.Record.Date.CompareTo(b.Record.Date);
        if (result == 0) result = Nullable.Compare(a.Record.EnteredAt, b.Record.EnteredAt);
        return result != 0 ? result : string.CompareOrdinal(a.Record.Id, b.Record.Id);
    }

    private static Comparison<IncomesTableRow> Comparison(IncomesQuery query)
    {
        Comparison<IncomesTableRow> key = query.Sort switch
        {
            "order" => (a, b) => string.Compare(a.Record.OrderNo, b.Record.OrderNo, StringComparison.OrdinalIgnoreCase),
            "branch" => (a, b) => Names.Compare(a.Branch.Name, b.Branch.Name),
            "method" => (a, b) => IncomesMethods.Rank(a.Record.Method).CompareTo(IncomesMethods.Rank(b.Record.Method)),
            "emp" => (a, b) => Names.Compare(a.Record.Person ?? "", b.Record.Person ?? ""),
            "cust" => (a, b) => Names.Compare(a.Record.Customer ?? "", b.Record.Customer ?? ""),
            "disc" => (a, b) => Nullable.Compare(a.Record.DiscountPct, b.Record.DiscountPct),
            "paid" => (a, b) => Nullable.Compare(a.Record.PaidEur, b.Record.PaidEur),
            "status" => (a, b) => a.Record.Cancelled.CompareTo(b.Record.Cancelled),
            "amt" => (a, b) => a.Record.Amount.CompareTo(b.Record.Amount),
            _ => ByDate,
        };
        var sign = query.SortDesc ? -1 : 1;
        return (a, b) =>
        {
            var result = sign * key(a, b);
            return result != 0 ? result : -ByDate(a, b);
        };
    }

    private static string GroupKey(IncomesQuery query, IncomesRecord record, IncomesBranch branch) => query.Group switch
    {
        "day" => IncomesPeriod.BucketKey(record.Date, "day"),
        "week" => IncomesPeriod.WeekKey(record.Date),
        "month" => IncomesPeriod.MonthKey(record.Date),
        "branch" => branch.Code,
        "method" => record.Method ?? "",
        "employee" or "consultant" => string.IsNullOrWhiteSpace(record.Person) ? NoPersonKey : record.Person.Trim(),
        _ => "",
    };

    private static string GroupLabel(IncomesQuery query, IncomesTableRow row) => query.Group switch
    {
        "branch" => row.Branch.Name,
        "method" => IncomesMethods.Name(row.GroupKey),
        "employee" or "consultant" => row.GroupKey.Length == 0 ? "—" : row.GroupKey,
        _ => row.GroupKey,
    };

    /// <summary>Days, weeks and months follow the date sort; methods keep their fixed order; the rest go by subtotal.</summary>
    private static List<IncomesGroup> OrderGroups(IncomesQuery query, List<IncomesGroup> groups) => query.Group switch
    {
        "day" or "week" or "month" => query.Sort == "date" && !query.SortDesc
            ? groups.OrderBy(g => g.Key, StringComparer.Ordinal).ToList()
            : groups.OrderByDescending(g => g.Key, StringComparer.Ordinal).ToList(),
        "method" => groups.OrderBy(g => IncomesMethods.Rank(g.Key)).ToList(),
        _ => groups.OrderByDescending(g => g.SumEur).ThenBy(g => g.Label, Names).ToList(),
    };
}
