namespace LKvitai.MES.Modules.Sales.Contracts.Reports;

/// <summary>
/// The three management reports of the legacy Mango workbook, live over LKvitaiDb:
/// production (sumReportProduction), work / accruals (selectSalaryAccuirance) and
/// payments received (PivotMenuo / AtaskaitaViliusMonthPinigai).
/// </summary>
public static class SalesReportNames
{
    public const string Production = "production";
    public const string Work = "work";
    public const string Payments = "payments";
}

/// <param name="Report">One of <see cref="SalesReportNames"/>.</param>
/// <param name="From">Inclusive start date.</param>
/// <param name="To">Exclusive end date.</param>
/// <param name="GroupBy">Dimension names to group by, in order (e.g. "period", "type").</param>
/// <param name="Granularity">"day", "month" or "year" for the "period" dimension.</param>
/// <param name="Filters">Dimension name → case-insensitive "contains" filter.</param>
public sealed record SalesReportQuery(
    string Report,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<string> GroupBy,
    string Granularity,
    IReadOnlyDictionary<string, string> Filters);

public sealed record SalesReportRow(
    IReadOnlyDictionary<string, string?> Keys,
    IReadOnlyDictionary<string, decimal> Values);

public sealed record SalesReportResult(
    string Report,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<string> GroupBy,
    string Granularity,
    IReadOnlyList<string> Measures,
    IReadOnlyList<SalesReportRow> Rows,
    bool Truncated);

/// <summary>The exact values each filterable dimension of a report can take.</summary>
public sealed record SalesReportDimensions(
    string Report,
    IReadOnlyList<string> Dimensions,
    IReadOnlyList<string> Measures,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Values);

public sealed class SalesReportException(string message) : Exception(message);

/// <summary>Operational lists (see <c>GET /api/sales/reports/lists/{name}</c>).</summary>
public static class SalesListReportNames
{
    public const string Pipeline = "pipeline";
    public const string Materials = "materials";
    public const string Stopped = "stopped";
    public const string UnpaidOrders = "unpaid-orders";
    public const string NotDelivered = "not-delivered";
    public const string CustomerDebt = "customer-debt";
    public const string OrderSearch = "order-search";
}

public sealed record SalesListResult(
    string List,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    bool Truncated);
