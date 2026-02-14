using System.Security.Claims;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/admin/compliance")]
public sealed class AdminComplianceController : ControllerBase
{
    private readonly ITransactionExportService _transactionExportService;
    private readonly ILotTraceabilityService _lotTraceabilityService;
    private readonly ILotTraceStore _lotTraceStore;
    private readonly IComplianceReportService _complianceReportService;

    public AdminComplianceController(
        ITransactionExportService transactionExportService,
        ILotTraceabilityService lotTraceabilityService,
        ILotTraceStore lotTraceStore,
        IComplianceReportService complianceReportService)
    {
        _transactionExportService = transactionExportService;
        _lotTraceabilityService = lotTraceabilityService;
        _lotTraceStore = lotTraceStore;
        _complianceReportService = complianceReportService;
    }

    [HttpGet("dashboard")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAuditor)]
    public async Task<IActionResult> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var result = await _complianceReportService.GetDashboardAsync(cancellationToken);
        return Ok(new ComplianceDashboardResponse(
            result.PendingExports,
            result.RecentTraces,
            result.VarianceAlerts,
            result.RecentReports.Select(MapHistory).ToList()));
    }

    [HttpGet("scheduled-reports")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAuditor)]
    public async Task<IActionResult> GetScheduledReportsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _complianceReportService.GetScheduledReportsAsync(cancellationToken);
        return Ok(rows);
    }

    [HttpPost("scheduled-reports")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAuditor)]
    public async Task<IActionResult> CreateScheduledReportAsync(
        [FromBody] ScheduledReportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return BadRequest(new { message = "Request body is required." });
        }

        if (!Enum.TryParse<ComplianceReportType>(request.ReportType, true, out var reportType))
        {
            return BadRequest(new { message = $"Unsupported reportType '{request.ReportType}'." });
        }

        if (!Enum.TryParse<ComplianceReportFormat>(request.Format, true, out var format))
        {
            return BadRequest(new { message = $"Unsupported format '{request.Format}'." });
        }

        var created = await _complianceReportService.CreateAsync(new ScheduledReport
        {
            ReportType = reportType,
            Schedule = request.Schedule,
            EmailRecipients = string.Join(",", request.EmailRecipients ?? new List<string>()),
            Format = format,
            Active = request.Active
        }, User.Identity?.Name ?? "system", cancellationToken);

        return CreatedAtAction(nameof(GetScheduledReportsAsync), created);
    }

    [HttpPut("scheduled-reports/{id:int}")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAuditor)]
    public async Task<IActionResult> UpdateScheduledReportAsync(
        int id,
        [FromBody] ScheduledReportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<ComplianceReportType>(request.ReportType, true, out var reportType) ||
            !Enum.TryParse<ComplianceReportFormat>(request.Format, true, out var format))
        {
            return BadRequest(new { message = "Invalid reportType or format." });
        }

        var updated = await _complianceReportService.UpdateAsync(id, new ScheduledReport
        {
            ReportType = reportType,
            Schedule = request.Schedule,
            EmailRecipients = string.Join(",", request.EmailRecipients ?? new List<string>()),
            Format = format,
            Active = request.Active
        }, cancellationToken);

        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("scheduled-reports/{id:int}")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAuditor)]
    public async Task<IActionResult> DeleteScheduledReportAsync(int id, CancellationToken cancellationToken = default)
    {
        var deleted = await _complianceReportService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("scheduled-reports/{id:int}/run")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAuditor)]
    public async Task<IActionResult> RunScheduledReportAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _complianceReportService.GetScheduledReportsAsync(cancellationToken);
        var schedule = existing.FirstOrDefault(x => x.Id == id);
        if (schedule is null)
        {
            return NotFound();
        }

        var history = await _complianceReportService.TriggerAsync(
            schedule.ReportType,
            schedule.Format,
            "MANUAL",
            schedule.Id,
            cancellationToken);

        return Ok(MapHistory(history));
    }

    [HttpGet("scheduled-reports/history")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAuditor)]
    public async Task<IActionResult> GetReportHistoryAsync([FromQuery] int? limit = null, CancellationToken cancellationToken = default)
    {
        var history = await _complianceReportService.GetHistoryAsync(limit, cancellationToken);
        return Ok(history.Select(MapHistory));
    }

    [HttpPost("export-transactions")]
    [Authorize(Policy = WarehousePolicies.AdminOrAuditor)]
    public async Task<IActionResult> ExportTransactionsAsync(
        [FromBody] ExportTransactionsRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return BadRequest(new { message = "Request body is required." });
        }

        if (!Enum.TryParse<TransactionExportFormat>(request.Format, true, out var format))
        {
            return BadRequest(new { message = $"Unsupported format '{request.Format}'. Expected CSV or JSON." });
        }

        TransactionExportSftpDestination? destination = null;
        if (request.SftpUpload)
        {
            if (request.Sftp is null)
            {
                return BadRequest(new { message = "SFTP configuration is required when sftpUpload=true." });
            }

            if (string.IsNullOrWhiteSpace(request.Sftp.Host) ||
                string.IsNullOrWhiteSpace(request.Sftp.Username) ||
                string.IsNullOrWhiteSpace(request.Sftp.Password))
            {
                return BadRequest(new { message = "SFTP host, username, and password are required." });
            }

            destination = new TransactionExportSftpDestination(
                request.Sftp.Host.Trim(),
                request.Sftp.Username.Trim(),
                request.Sftp.Password,
                string.IsNullOrWhiteSpace(request.Sftp.Path) ? null : request.Sftp.Path.Trim(),
                request.Sftp.DeleteLocalAfterUpload);
        }

        var exportedBy =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue(ClaimTypes.Name) ??
            "system";

        var result = await _transactionExportService.ExportAsync(
            new TransactionExportExecuteCommand(
                request.StartDate,
                request.EndDate,
                format,
                exportedBy,
                destination),
            cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                exportId = result.ExportId,
                status = result.Status.ToString(),
                error = result.ErrorMessage
            });
        }

        return Ok(new
        {
            exportId = result.ExportId,
            rowCount = result.RowCount,
            format = format.ToString().ToUpperInvariant(),
            filePaths = result.FilePaths,
            status = result.Status.ToString().ToUpperInvariant()
        });
    }

    [HttpGet("exports")]
    [Authorize(Policy = WarehousePolicies.AdminOrAuditor)]
    public async Task<IActionResult> GetExportsAsync(
        [FromQuery] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await _transactionExportService.GetHistoryAsync(limit, cancellationToken);

        return Ok(rows.Select(x => new ExportHistoryResponse(
            x.ExportId,
            x.StartDate,
            x.EndDate,
            x.Format.ToString().ToUpperInvariant(),
            x.RowCount,
            x.FilePaths,
            x.Status.ToString().ToUpperInvariant(),
            x.ErrorMessage,
            x.ExportedBy,
            x.ExportedAt)));
    }

    [HttpPost("lot-trace")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAuditor)]
    public async Task<IActionResult> BuildLotTraceAsync(
        [FromBody] LotTraceRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return BadRequest(new { message = "Request body is required." });
        }

        if (!Enum.TryParse<LotTraceDirection>(request.Direction, true, out var direction))
        {
            return BadRequest(new { message = $"Unsupported direction '{request.Direction}'. Expected BACKWARD or FORWARD." });
        }

        var result = await _lotTraceabilityService.BuildAsync(request.LotNumber, direction, cancellationToken);
        if (!result.Succeeded || result.Report is null)
        {
            return result.StatusCode == 404
                ? NotFound(new { message = result.ErrorMessage })
                : BadRequest(new { message = result.ErrorMessage });
        }

        _lotTraceStore.Save(result.Report);

        var format = string.IsNullOrWhiteSpace(request.Format) ? "JSON" : request.Format.Trim().ToUpperInvariant();
        if (format == "CSV")
        {
            var csv = _lotTraceabilityService.BuildCsv(result.Report);
            var fileName = $"lot-trace-{result.Report.LotNumber}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
            return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
        }

        return Ok(ToLotTraceResponse(result.Report));
    }

    [HttpGet("lot-trace/{traceId:guid}")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAuditor)]
    public IActionResult GetLotTraceAsync(Guid traceId)
    {
        if (!_lotTraceStore.TryGet(traceId, out var report) || report is null)
        {
            return NotFound(new { message = "Trace report not found." });
        }

        return Ok(ToLotTraceResponse(report));
    }

    private static LotTraceResponse ToLotTraceResponse(LotTraceReport report)
    {
        return new LotTraceResponse(
            report.TraceId,
            report.LotNumber,
            report.Direction.ToString().ToUpperInvariant(),
            report.IsApproximate,
            report.GeneratedAt,
            ToNode(report.Root));
    }

    private static LotTraceNodeResponse ToNode(LotTraceNode node)
    {
        return new LotTraceNodeResponse(
            node.NodeType,
            node.NodeId,
            node.NodeName,
            node.Timestamp,
            node.Children.Select(ToNode).ToList());
    }

    public sealed record ExportTransactionsRequest(
        DateTimeOffset StartDate,
        DateTimeOffset EndDate,
        string Format,
        bool SftpUpload = false,
        SftpRequest? Sftp = null);

    public sealed record SftpRequest(
        string Host,
        string Username,
        string Password,
        string? Path,
        bool DeleteLocalAfterUpload = true);

    public sealed record ExportHistoryResponse(
        Guid ExportId,
        DateTimeOffset StartDate,
        DateTimeOffset EndDate,
        string Format,
        int RowCount,
        IReadOnlyList<string> FilePaths,
        string Status,
        string? ErrorMessage,
        string ExportedBy,
        DateTimeOffset ExportedAt);

    public sealed record LotTraceRequest(
        string LotNumber,
        string Direction,
        string? Format = "JSON");

    public sealed record LotTraceResponse(
        Guid TraceId,
        string LotNumber,
        string Direction,
        bool IsApproximate,
        DateTimeOffset GeneratedAt,
        LotTraceNodeResponse Root);

    public sealed record LotTraceNodeResponse(
        string NodeType,
        string NodeId,
        string NodeName,
        DateTimeOffset Timestamp,
        IReadOnlyList<LotTraceNodeResponse> Children);

    public sealed record ScheduledReportRequest(
        string ReportType,
        string Schedule,
        List<string>? EmailRecipients,
        string Format,
        bool Active = true);

    public sealed record ComplianceDashboardResponse(
        int PendingExports,
        int RecentTraces,
        int VarianceAlerts,
        IReadOnlyList<ReportHistoryResponse> RecentReports);

    public sealed record ReportHistoryResponse(
        Guid Id,
        int? ScheduledReportId,
        string ReportType,
        string Format,
        string Status,
        string Trigger,
        string FilePath,
        string? ErrorMessage,
        DateTimeOffset GeneratedAt);

    private static ReportHistoryResponse MapHistory(GeneratedReportHistory history) =>
        new ReportHistoryResponse(
            history.Id,
            history.ScheduledReportId,
            history.ReportType.ToString().ToUpperInvariant(),
            history.Format.ToString().ToUpperInvariant(),
            history.Status.ToString().ToUpperInvariant(),
            history.Trigger,
            history.FilePath,
            history.ErrorMessage,
            history.GeneratedAt);
}
