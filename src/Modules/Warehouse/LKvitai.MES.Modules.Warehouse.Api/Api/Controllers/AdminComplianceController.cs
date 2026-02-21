using System.Linq;
using System.Security.Claims;
using System.Text;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/admin/compliance")]
public sealed class AdminComplianceController : ControllerBase
{
    private readonly ITransactionExportService _transactionExportService;
    private readonly ILotTraceabilityService _lotTraceabilityService;
    private readonly ILotTraceStore _lotTraceStore;
    private readonly IComplianceReportService _complianceReportService;
    private readonly IElectronicSignatureService _signatureService;

    public AdminComplianceController(
        ITransactionExportService transactionExportService,
        ILotTraceabilityService lotTraceabilityService,
        ILotTraceStore lotTraceStore,
        IComplianceReportService complianceReportService,
        IElectronicSignatureService signatureService)
    {
        _transactionExportService = transactionExportService;
        _lotTraceabilityService = lotTraceabilityService;
        _lotTraceStore = lotTraceStore;
        _complianceReportService = complianceReportService;
        _signatureService = signatureService;
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

    [HttpPost("sign")]
    [Authorize]
    public async Task<IActionResult> CaptureSignatureAsync([FromBody] CaptureSignatureRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return BadRequest(new { message = "Request body is required." });
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        try
        {
            var signature = await _signatureService.CaptureAsync(
                new CaptureSignatureCommand(
                    request.Action,
                    request.ResourceId,
                    request.SignatureText,
                    request.Meaning,
                    userId,
                    ip,
                    request.Password),
                cancellationToken);

            return CreatedAtAction(nameof(GetSignaturesForResourceAsync), new { resourceId = signature.ResourceId }, new SignatureResponse(
                signature.Id,
                signature.UserId,
                signature.Action,
                signature.ResourceId,
                signature.SignatureText,
                signature.Meaning,
                signature.Timestamp,
                signature.IpAddress,
                signature.PreviousHash,
                signature.CurrentHash));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("signatures/{resourceId}")]
    [Authorize]
    public async Task<IActionResult> GetSignaturesForResourceAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        var rows = await _signatureService.GetByResourceAsync(resourceId, cancellationToken);
        return Ok(rows.Select(x => new SignatureResponse(
            x.Id,
            x.UserId,
            x.Action,
            x.ResourceId,
            x.SignatureText,
            x.Meaning,
            x.Timestamp,
            x.IpAddress,
            x.PreviousHash,
            x.CurrentHash)));
    }

    [HttpPost("verify-hash-chain")]
    [Authorize]
    public async Task<IActionResult> VerifyHashChainAsync(CancellationToken cancellationToken = default)
    {
        var result = await _signatureService.VerifyHashChainAsync(cancellationToken);
        return Ok(new
        {
            valid = result.Valid,
            signatureCount = result.SignatureCount,
            error = result.ErrorMessage
        });
    }

    [HttpGet("validation-report")]
    [Authorize(Policy = WarehousePolicies.AdminOnly)]
    public async Task<IActionResult> GenerateValidationReportAsync(CancellationToken cancellationToken = default)
    {
        var verify = await _signatureService.VerifyHashChainAsync(cancellationToken);

        var lines = new List<string>
        {
            "FDA 21 CFR Part 11 Validation Report",
            $"GeneratedAt: {DateTimeOffset.UtcNow:O}",
            $"HashChainValid: {verify.Valid}",
            $"SignatureCount: {verify.SignatureCount}",
            $"Error: {verify.ErrorMessage ?? "none"}"
        };

        var content = SimplePdfBuilder.BuildSinglePage("FDA 21 CFR Part 11", lines);
        return File(content, "application/pdf", "validation-report.pdf");
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

    public sealed record CaptureSignatureRequest(
        string Action,
        string ResourceId,
        string SignatureText,
        string Meaning,
        string Password);

    public sealed record SignatureResponse(
        long Id,
        string UserId,
        string Action,
        string ResourceId,
        string SignatureText,
        string Meaning,
        DateTimeOffset Timestamp,
        string IpAddress,
        string PreviousHash,
        string CurrentHash);

    private static class SimplePdfBuilder
    {
        public static byte[] BuildSinglePage(string title, IReadOnlyList<string> lines)
        {
            using var stream = new MemoryStream();

            static void WriteLine(MemoryStream ms, string text)
            {
                var bytes = Encoding.ASCII.GetBytes(text + "\n");
                ms.Write(bytes, 0, bytes.Length);
            }

            static string Escape(string value)
                => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

            var contentBuilder = new StringBuilder();
            contentBuilder.Append("BT /F1 11 Tf 40 800 Td ");
            contentBuilder.Append($"({Escape(title)}) Tj 0 -18 Td ");
            foreach (var line in lines.Take(42))
            {
                contentBuilder.Append($"({Escape(line)}) Tj 0 -14 Td ");
            }

            contentBuilder.Append("ET");
            var content = Encoding.ASCII.GetBytes(contentBuilder.ToString());

            WriteLine(stream, "%PDF-1.4");

            var offsets = new Dictionary<int, long>();

            offsets[1] = stream.Position;
            WriteLine(stream, "1 0 obj");
            WriteLine(stream, "<< /Type /Catalog /Pages 2 0 R >>");
            WriteLine(stream, "endobj");

            offsets[2] = stream.Position;
            WriteLine(stream, "2 0 obj");
            WriteLine(stream, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
            WriteLine(stream, "endobj");

            offsets[3] = stream.Position;
            WriteLine(stream, "3 0 obj");
            WriteLine(stream, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>");
            WriteLine(stream, "endobj");

            offsets[4] = stream.Position;
            WriteLine(stream, "4 0 obj");
            WriteLine(stream, $"<< /Length {content.Length} >>");
            WriteLine(stream, "stream");
            stream.Write(content, 0, content.Length);
            WriteLine(stream, string.Empty);
            WriteLine(stream, "endstream");
            WriteLine(stream, "endobj");

            offsets[5] = stream.Position;
            WriteLine(stream, "5 0 obj");
            WriteLine(stream, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
            WriteLine(stream, "endobj");

            var xrefPosition = stream.Position;
            WriteLine(stream, "xref");
            WriteLine(stream, "0 6");
            WriteLine(stream, "0000000000 65535 f ");
            for (var i = 1; i <= 5; i++)
            {
                WriteLine(stream, $"{offsets[i]:D10} 00000 n ");
            }

            WriteLine(stream, "trailer");
            WriteLine(stream, "<< /Size 6 /Root 1 0 R >>");
            WriteLine(stream, "startxref");
            WriteLine(stream, xrefPosition.ToString());
            WriteLine(stream, "%%EOF");

            return stream.ToArray();
        }
    }
}
