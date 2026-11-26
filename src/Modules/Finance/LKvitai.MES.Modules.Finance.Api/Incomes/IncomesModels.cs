namespace LKvitai.MES.Modules.Finance.Api.Incomes;

public enum IncomesDataset { Money, Orders }

/// <summary>The four legacy payment methods (dbo.Zinynas_mokejimotipai), in the fixed order the report uses.</summary>
public static class IncomesMethods
{
    public const string Bank = "BANK";
    public const string Eka = "EKA";
    public const string Ppk = "PPK";
    public const string Kasa = "KASA";
    public static IReadOnlyList<string> All { get; } = [Bank, Eka, Ppk, Kasa];

    /// <summary>Legacy MokejimoTipasID values, used in the SQL filter.</summary>
    public const string LegacyIds = "1, 2, 81423987, 834019474";

    public static string? FromLegacyId(int id) => id switch
    {
        1 => Ppk,
        2 => Bank,
        81423987 => Kasa,
        834019474 => Eka,
        _ => null,
    };

    public static string Name(string code) => code switch
    {
        Bank => "Pavedimu",
        Eka => "Per EKA",
        Ppk => "Per PPK",
        Kasa => "Per kasą",
        _ => code,
    };

    public static string Hint(string code) => code switch
    {
        Bank => "banko pavedimas",
        Eka => "kasos aparatas (grynieji / kortelė)",
        Ppk => "grynieji su pinigų priėmimo kvitu",
        Kasa => "grynieji kasoje",
        _ => "",
    };

    public static int Rank(string? code) => code switch
    {
        Bank => 0,
        Eka => 1,
        Ppk => 2,
        Kasa => 3,
        _ => int.MaxValue,
    };
}

/// <summary>A branch from dbo.Zinynas_filialai (the test branch excluded).</summary>
public sealed record IncomesBranch(int Id, string Code, string Name);

/// <summary>
/// One row of either dataset. Money received: one payment (dbo.Mokejimai or MTTools.dbo.LKUZAM).
/// Orders taken: one order (dbo.Uzsakymai), <see cref="Amount"/> is its value after discount
/// (for an order cancelled at the client's request, the amount the client still pays).
/// </summary>
public sealed record IncomesRecord(
    string Id,
    DateOnly Date,
    DateTime? EnteredAt,
    string OrderNo,
    int BranchId,
    string Series,
    string? Method,
    decimal Amount,
    string? Person,
    string? Note,
    string? PpkNo = null,
    int? BankTransferNo = null,
    int? EkaReceiptNo = null,
    string? Customer = null,
    decimal? DiscountPct = null,
    decimal? PaidEur = null,
    bool Cancelled = false);

/// <summary>The parsed query string shared by summary, rows and export (the page URL uses the same names).</summary>
public sealed record IncomesQuery
{
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    public IncomesDataset Dataset { get; init; } = IncomesDataset.Money;
    /// <summary>Branch codes; null = all.</summary>
    public IReadOnlySet<string>? Branches { get; init; }
    /// <summary>Method codes; null = all. Ignored for orders.</summary>
    public IReadOnlySet<string>? Methods { get; init; }
    /// <summary>Chart buckets: day, week or month.</summary>
    public string Bucket { get; init; } = "day";

    public DateOnly? Day { get; init; }
    public int? Week { get; init; }
    public string? Month { get; init; }
    public string? DrillBranch { get; init; }
    public string? DrillMethod { get; init; }
    public bool Refunds { get; init; }
    public bool NoEmployee { get; init; }
    public bool CancelledOnly { get; init; }
    public string Search { get; init; } = "";
    public string Sort { get; init; } = "date";
    public bool SortDesc { get; init; } = true;
    public string Group { get; init; } = "none";
    public int Skip { get; init; }
    public int Take { get; init; } = 100;

    public bool IsMoney => Dataset == IncomesDataset.Money;
    public int Days => To.DayNumber - From.DayNumber + 1;
    public bool HasDrill => Day is not null || Week is not null || Month is not null || DrillBranch is not null ||
                            DrillMethod is not null || Refunds || NoEmployee || CancelledOnly;
}

public sealed class IncomesQueryException(string message) : Exception(message);

public sealed record IncomesPeriodInfo(DateOnly From, DateOnly To, string Bucket, int Days, int Workdays);
public sealed record IncomesBranchInfo(string Code, string Name, int Count, decimal TotalEur, bool HasData);
public sealed record IncomesMethodInfo(string Code, string Name, string Hint, int Count);
public sealed record IncomesCountSum(int Count, decimal AmountEur);
public sealed record IncomesTotals(int Count, decimal AmountEur, IncomesCountSum Refunds, int NoEmployeeCount, IncomesCountSum Cancelled);
public sealed record IncomesComparison(DateOnly From, DateOnly To, int Count, decimal AmountEur, int RefundsCount, decimal RefundsEur);
public sealed record IncomesComparisons(IncomesComparison Previous, IncomesComparison LastYear);
public sealed record IncomesBucket(string Key, DateOnly From, DateOnly To, int Count, decimal AmountEur,
    IReadOnlyDictionary<string, decimal> PositiveByMethod, IReadOnlyDictionary<string, decimal> PositiveByBranch);
/// <summary>A matrix column: a method (money) or an ISO week / month of the period (orders).</summary>
public sealed record IncomesColumn(string Key, string Label, string? Sub, DateOnly? From, DateOnly? To);
public sealed record IncomesMatrixCell(string Branch, string Column, int Count, decimal AmountEur);
public sealed record IncomesSoldVsReceived(string Branch, decimal SoldEur, decimal ReceivedEur);
/// <summary>Drill filter a remark (or a KPI link) applies to the table.</summary>
public sealed record IncomesDrill(DateOnly? Day = null, string? Branch = null, string? Method = null,
    bool Refunds = false, bool NoEmployee = false, bool Cancelled = false);
/// <summary>
/// An automatic remark. <see cref="Kind"/> picks the sentence; <see cref="Values"/> are its numbers and names
/// (invariant culture), so the page and the Excel export word them in their own language.
/// </summary>
public sealed record IncomesRemark(string Kind, string Tone, string Glyph, IReadOnlyDictionary<string, string> Values,
    IncomesDrill? Drill);

public sealed record IncomesSummary(
    IncomesPeriodInfo Period,
    string Dataset,
    IReadOnlyList<IncomesBranchInfo> Branches,
    IReadOnlyList<IncomesMethodInfo> Methods,
    IncomesTotals Totals,
    IncomesComparisons Comparisons,
    IReadOnlyList<IncomesBucket> Buckets,
    IReadOnlyList<IncomesColumn> Columns,
    IReadOnlyList<IncomesMatrixCell> Matrix,
    IReadOnlyList<IncomesSoldVsReceived> SoldVsReceived,
    IReadOnlyList<IncomesRemark> Remarks,
    DateOnly Today);

public sealed record IncomesGroup(string Key, string Label, int Count, decimal SumEur);

/// <summary>A table row. Money rows fill the payment fields, order rows the order fields.</summary>
public sealed record IncomesRow(
    string Id,
    DateOnly Date,
    DateTime? EnteredAt,
    string OrderNo,
    string Branch,
    string BranchCode,
    string Series,
    string? Method = null,
    decimal? AmountEur = null,
    string? Employee = null,
    string? Note = null,
    string? PpkNo = null,
    int? BankTransferNo = null,
    int? EkaReceiptNo = null,
    string? Consultant = null,
    string? Customer = null,
    decimal? ValueEur = null,
    decimal? DiscountPct = null,
    decimal? PaidEur = null,
    string? Status = null);

public sealed record IncomesRowsPage(int Total, decimal SumEur, decimal RefundsEur, IReadOnlyList<IncomesGroup> Groups,
    IReadOnlyList<IncomesRow> Items);
