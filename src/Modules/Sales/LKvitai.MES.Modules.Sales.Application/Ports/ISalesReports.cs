using LKvitai.MES.Modules.Sales.Contracts.Reports;

namespace LKvitai.MES.Modules.Sales.Application.Ports;

/// <summary>
/// Read-only management reports over the legacy database, run under the caller's own
/// legacy SQL login so the legacy permissions on payments and accruals still apply.
/// </summary>
public interface ISalesReports
{
    Task<SalesReportResult> RunAsync(SalesReportQuery query, CancellationToken cancellationToken);

    Task<SalesReportDimensions> DimensionsAsync(string report, CancellationToken cancellationToken);
}

/// <summary>Operational lists (queue, stopped, unpaid, not delivered, debt, order search), same identity rules as <see cref="ISalesReports"/>.</summary>
public interface ISalesListReports
{
    Task<SalesListResult> RunAsync(string name, IReadOnlyDictionary<string, string> args, CancellationToken cancellationToken);
}
