using System.Security.Claims;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/admin/compliance")]
[Authorize(Policy = WarehousePolicies.AdminOrAuditor)]
public sealed class AdminComplianceController : ControllerBase
{
    private readonly ITransactionExportService _transactionExportService;

    public AdminComplianceController(ITransactionExportService transactionExportService)
    {
        _transactionExportService = transactionExportService;
    }

    [HttpPost("export-transactions")]
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
}
