namespace LKvitai.MES.Modules.Finance.WebUI.IncomesReport;

// JSON of GET /api/finance/incomes/summary and /rows (Finance.Api, Incomes/IncomesModels.cs).

public sealed record IncomesPeriodDto(DateOnly From, DateOnly To, string Bucket, int Days, int Workdays);
public sealed record IncomesBranchDto(string Code, string Name, int Count, decimal TotalEur, bool HasData);
public sealed record IncomesMethodDto(string Code, string Name, string Hint, int Count);
public sealed record IncomesCountSumDto(int Count, decimal AmountEur);
public sealed record IncomesTotalsDto(int Count, decimal AmountEur, IncomesCountSumDto Refunds, int NoEmployeeCount,
    IncomesCountSumDto Cancelled);
public sealed record IncomesComparisonDto(DateOnly From, DateOnly To, int Count, decimal AmountEur, int RefundsCount, decimal RefundsEur);
public sealed record IncomesComparisonsDto(IncomesComparisonDto Previous, IncomesComparisonDto LastYear);
public sealed record IncomesBucketDto(string Key, DateOnly From, DateOnly To, int Count, decimal AmountEur,
    Dictionary<string, decimal> PositiveByMethod, Dictionary<string, decimal> PositiveByBranch);
public sealed record IncomesColumnDto(string Key, string Label, string? Sub, DateOnly? From, DateOnly? To);
public sealed record IncomesCellDto(string Branch, string Column, int Count, decimal AmountEur);
public sealed record IncomesFlowDto(string Branch, decimal SoldEur, decimal ReceivedEur);
public sealed record IncomesDrillDto(DateOnly? Day, string? Branch, string? Method, bool Refunds, bool NoEmployee, bool Cancelled);
public sealed record IncomesRemarkDto(string Kind, string Tone, string Glyph, Dictionary<string, string> Values, IncomesDrillDto? Drill);

public sealed record IncomesSummaryDto(
    IncomesPeriodDto Period,
    string Dataset,
    List<IncomesBranchDto> Branches,
    List<IncomesMethodDto> Methods,
    IncomesTotalsDto Totals,
    IncomesComparisonsDto Comparisons,
    List<IncomesBucketDto> Buckets,
    List<IncomesColumnDto> Columns,
    List<IncomesCellDto> Matrix,
    List<IncomesFlowDto> SoldVsReceived,
    List<IncomesRemarkDto> Remarks,
    DateOnly Today);

public sealed record IncomesGroupDto(string Key, string Label, int Count, decimal SumEur);

public sealed record IncomesRowDto(
    string Id,
    DateOnly Date,
    DateTime? EnteredAt,
    string OrderNo,
    string Branch,
    string BranchCode,
    string Series,
    string? Method,
    decimal? AmountEur,
    string? Employee,
    string? Note,
    string? PpkNo,
    int? BankTransferNo,
    int? EkaReceiptNo,
    string? Consultant,
    string? Customer,
    decimal? ValueEur,
    decimal? DiscountPct,
    decimal? PaidEur,
    string? Status)
{
    public decimal Amount => AmountEur ?? ValueEur ?? 0;
    public bool IsCancelled => Status == "cancelled";
}

public sealed record IncomesRowsPageDto(int Total, decimal SumEur, decimal RefundsEur, List<IncomesGroupDto> Groups,
    List<IncomesRowDto> Items);

/// <summary>Why a request failed, for the error panel (endpoint · status · time · correlation id).</summary>
public sealed record IncomesFailure(int Status, string Path, string At, string Id);

/// <summary>One item of the virtualized table: a group header or a row (null while its page is loading).</summary>
public sealed record IncomesFlatItem(IncomesGroupDto? Group, IncomesRowDto? Row, bool Collapsed);
