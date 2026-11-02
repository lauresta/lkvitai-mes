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
