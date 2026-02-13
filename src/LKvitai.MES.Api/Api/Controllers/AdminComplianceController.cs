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

    public AdminComplianceController(
        ITransactionExportService transactionExportService,
        ILotTraceabilityService lotTraceabilityService,
        ILotTraceStore lotTraceStore)
    {
        _transactionExportService = transactionExportService;
        _lotTraceabilityService = lotTraceabilityService;
        _lotTraceStore = lotTraceStore;
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
}
