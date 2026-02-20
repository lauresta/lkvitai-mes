using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using Cronos;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public sealed class ComplianceReportOptions
{
    public string ReportRootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "exports", "compliance-reports");
    public SmtpOptions Smtp { get; set; } = new();
}

public sealed class SmtpOptions
{
    public bool Enabled { get; set; } = false;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 25;
    public bool UseSsl { get; set; } = false;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = "noreply@warehouse.local";
}

public sealed record ComplianceDashboardSummary(
    int PendingExports,
    int RecentTraces,
    int VarianceAlerts,
    IReadOnlyList<GeneratedReportHistory> RecentReports);

public interface IComplianceReportService
{
    Task<ComplianceDashboardSummary> GetDashboardAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScheduledReport>> GetScheduledReportsAsync(CancellationToken cancellationToken = default);

    Task<ScheduledReport> CreateAsync(ScheduledReport request, string createdBy, CancellationToken cancellationToken = default);

    Task<ScheduledReport?> UpdateAsync(int id, ScheduledReport request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<GeneratedReportHistory> TriggerAsync(
        ComplianceReportType type,
        ComplianceReportFormat format,
        string trigger,
        int? scheduledReportId = null,
        CancellationToken cancellationToken = default);

    Task<int> ProcessDueSchedulesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GeneratedReportHistory>> GetHistoryAsync(int? limit, CancellationToken cancellationToken = default);
}

public sealed class ComplianceReportService : IComplianceReportService
{
    private readonly WarehouseDbContext _dbContext;
    private readonly ITransactionExportService _transactionExportService;
    private readonly ILotTraceStore _lotTraceStore;
    private readonly ILogger<ComplianceReportService> _logger;
    private readonly ComplianceReportOptions _options;

    public ComplianceReportService(
        WarehouseDbContext dbContext,
        ITransactionExportService transactionExportService,
        ILotTraceStore lotTraceStore,
        IOptions<ComplianceReportOptions> options,
        ILogger<ComplianceReportService> logger)
    {
        _dbContext = dbContext;
        _transactionExportService = transactionExportService;
        _lotTraceStore = lotTraceStore;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<ComplianceDashboardSummary> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var pendingExports = await _dbContext.TransactionExports
            .AsNoTracking()
            .CountAsync(x => x.Status == TransactionExportStatus.Pending, cancellationToken);

        var recentTraces = 0;
        if (_lotTraceStore is InMemoryLotTraceStore inMemory)
        {
            recentTraces = inMemory.Count;
        }

        var varianceAlerts = await _dbContext.CycleCountLines
            .AsNoTracking()
            .CountAsync(x => Math.Abs(x.Delta) > 0 || Math.Abs(x.PhysicalQty - x.SystemQty) > 0.01m, cancellationToken);

        var recentReports = await _dbContext.GeneratedReportHistories
            .AsNoTracking()
            .OrderByDescending(x => x.GeneratedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        return new ComplianceDashboardSummary(
            pendingExports,
            recentTraces,
            varianceAlerts,
            recentReports);
    }

    public Task<IReadOnlyList<ScheduledReport>> GetScheduledReportsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<ScheduledReport>>(
            _dbContext.ScheduledReports
                .AsNoTracking()
                .OrderBy(x => x.Id)
                .ToList());
    }

    public async Task<ScheduledReport> CreateAsync(ScheduledReport request, string createdBy, CancellationToken cancellationToken = default)
    {
        var entity = new ScheduledReport
        {
            ReportType = request.ReportType,
            Schedule = request.Schedule.Trim(),
            EmailRecipients = request.EmailRecipients?.Trim() ?? string.Empty,
            Format = request.Format,
            Active = request.Active,
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow,
            LastStatus = ComplianceReportStatus.Pending
        };

        _dbContext.ScheduledReports.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<ScheduledReport?> UpdateAsync(int id, ScheduledReport request, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.ScheduledReports.FindAsync(new object[] { id }, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        existing.ReportType = request.ReportType;
        existing.Schedule = request.Schedule.Trim();
        existing.EmailRecipients = request.EmailRecipients?.Trim() ?? string.Empty;
        existing.Format = request.Format;
        existing.Active = request.Active;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.ScheduledReports.FindAsync(new object[] { id }, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        _dbContext.ScheduledReports.Remove(existing);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<GeneratedReportHistory> TriggerAsync(
        ComplianceReportType type,
        ComplianceReportFormat format,
        string trigger,
        int? scheduledReportId = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_options.ReportRootPath);

        var history = new GeneratedReportHistory
        {
            ScheduledReportId = scheduledReportId,
            ReportType = type,
            Format = format,
            Trigger = trigger.ToUpperInvariant(),
            GeneratedAt = DateTimeOffset.UtcNow,
            Status = ComplianceReportStatus.InProgress
        };

        _dbContext.GeneratedReportHistories.Add(history);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            string filePath = format == ComplianceReportFormat.Csv
                ? await GenerateCsvAsync(type, history.Id, cancellationToken)
                : await GeneratePdfAsync(type, history.Id, cancellationToken);

            history.FilePath = filePath;
            history.Status = ComplianceReportStatus.Completed;
            history.ErrorMessage = null;
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (scheduledReportId.HasValue)
            {
                await UpdateScheduleStatusAsync(scheduledReportId.Value, ComplianceReportStatus.Completed, null, cancellationToken);
            }

            await TrySendEmailAsync(type, filePath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Compliance report generation failed. Type={ReportType} Trigger={Trigger}", type, trigger);
            history.Status = ComplianceReportStatus.Failed;
            history.ErrorMessage = ex.Message;
            await _dbContext.SaveChangesAsync(cancellationToken);

            if (scheduledReportId.HasValue)
            {
                await UpdateScheduleStatusAsync(scheduledReportId.Value, ComplianceReportStatus.Failed, ex.Message, cancellationToken);
            }
        }

        return history;
    }

    public async Task<int> ProcessDueSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var due = await _dbContext.ScheduledReports
            .AsNoTracking()
            .Where(x => x.Active)
            .ToListAsync(cancellationToken);

        var processed = 0;
        foreach (var schedule in due)
        {
            try
            {
                var cron = CronExpression.Parse(schedule.Schedule);
                var last = schedule.LastRunAt ?? schedule.CreatedAt.AddMinutes(-1);
                var next = cron.GetNextOccurrence(last.UtcDateTime, TimeZoneInfo.Utc);
                if (!next.HasValue || next.Value > now.UtcDateTime)
                {
                    continue;
                }

                processed++;
                await TriggerAsync(schedule.ReportType, schedule.Format, "SCHEDULED", schedule.Id, cancellationToken);
                await UpdateScheduleStatusAsync(schedule.Id, ComplianceReportStatus.Completed, null, cancellationToken, now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process scheduled report {ScheduleId}", schedule.Id);
                await UpdateScheduleStatusAsync(schedule.Id, ComplianceReportStatus.Failed, ex.Message, cancellationToken);
            }
        }

        return processed;
    }

    public async Task<IReadOnlyList<GeneratedReportHistory>> GetHistoryAsync(int? limit, CancellationToken cancellationToken = default)
    {
        IQueryable<GeneratedReportHistory> query = _dbContext.GeneratedReportHistories
            .AsNoTracking();

        if (limit.HasValue)
        {
            query = query.Take(limit.Value);
        }

        return await query
            .OrderByDescending(x => x.GeneratedAt)
            .ToListAsync(cancellationToken);
    }

    private async Task UpdateScheduleStatusAsync(
        int scheduleId,
        ComplianceReportStatus status,
        string? error,
        CancellationToken cancellationToken,
        DateTimeOffset? runAt = null)
    {
        var schedule = await _dbContext.ScheduledReports.FindAsync(new object[] { scheduleId }, cancellationToken);
        if (schedule is null)
        {
            return;
        }

        schedule.LastRunAt = runAt ?? DateTimeOffset.UtcNow;
        schedule.LastStatus = status;
        schedule.LastError = error;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> GenerateCsvAsync(ComplianceReportType type, Guid historyId, CancellationToken cancellationToken)
    {
        return type switch
        {
            ComplianceReportType.TransactionExport => await GenerateTransactionCsvAsync(cancellationToken),
            ComplianceReportType.VarianceAnalysis => await GenerateVarianceCsvAsync(cancellationToken),
            ComplianceReportType.LotTrace => await GenerateLotTraceCsvAsync(cancellationToken),
            _ => throw new InvalidOperationException($"Unsupported report type {type}")
        };
    }

    private async Task<string> GeneratePdfAsync(ComplianceReportType type, Guid historyId, CancellationToken cancellationToken)
    {
        var fileName = $"{type.ToString().ToLowerInvariant()}-{historyId:N}.pdf";
        var filePath = Path.Combine(_options.ReportRootPath, fileName);
        var lines = new List<string>();

        switch (type)
        {
            case ComplianceReportType.TransactionExport:
                lines.Add("Transaction export scheduled report");
                break;
            case ComplianceReportType.VarianceAnalysis:
                lines.Add("Variance analysis scheduled report");
                break;
            case ComplianceReportType.LotTrace:
                lines.Add("Lot trace scheduled report");
                break;
        }

        var content = SimplePdfBuilder.BuildSinglePage($"{type} Report", lines);
        await File.WriteAllBytesAsync(filePath, content, cancellationToken);
        return filePath;
    }

    private async Task<string> GenerateTransactionCsvAsync(CancellationToken cancellationToken)
    {
        var result = await _transactionExportService.ExportAsync(
            new TransactionExportExecuteCommand(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow,
                TransactionExportFormat.Csv,
                "system-scheduler",
                null),
            cancellationToken);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Transaction export failed.");
        }

        return result.FilePaths.First();
    }

    private async Task<string> GenerateVarianceCsvAsync(CancellationToken cancellationToken)
    {
        var lines = await BuildVarianceRowsAsync(cancellationToken);
        var fileName = $"variance-analysis-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        var filePath = Path.Combine(_options.ReportRootPath, fileName);

        var sb = new StringBuilder();
        sb.AppendLine("Location,ItemId,SKU,Delta,PhysicalQty,SystemQty,CountedAt");
        foreach (var line in lines)
        {
            sb.AppendLine($"{Escape(line.Location)},{line.ItemId},{Escape(line.Sku)},{line.Delta},{line.PhysicalQty},{line.SystemQty},{line.CountedAt:O}");
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), cancellationToken);
        return filePath;
    }

    private async Task<string> GenerateLotTraceCsvAsync(CancellationToken cancellationToken)
    {
        if (_lotTraceStore is not InMemoryLotTraceStore store || !store.TryGetAny(out var report) || report is null)
        {
            var fileName = $"lot-trace-empty-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
            var filePath = Path.Combine(_options.ReportRootPath, fileName);
            await File.WriteAllTextAsync(filePath, "No lot trace reports available", cancellationToken);
            return filePath;
        }

        var csv = new StringBuilder();
        csv.AppendLine("TraceId,LotNumber,Direction,GeneratedAt");
        csv.AppendLine($"{report.TraceId},{report.LotNumber},{report.Direction},{report.GeneratedAt:O}");
        return await WriteTextAsync(csv.ToString(), $"lot-trace-{DateTime.UtcNow:yyyyMMddHHmmss}.csv", cancellationToken);
    }

    private async Task<string> WriteTextAsync(string content, string fileName, CancellationToken cancellationToken)
    {
        var filePath = Path.Combine(_options.ReportRootPath, fileName);
        await File.WriteAllTextAsync(filePath, content, cancellationToken);
        return filePath;
    }

    private async Task TrySendEmailAsync(ComplianceReportType type, string filePath, CancellationToken cancellationToken)
    {
        if (!_options.Smtp.Enabled || string.IsNullOrWhiteSpace(_options.Smtp.Host))
        {
            _logger.LogInformation("SMTP disabled - skipping email delivery for {ReportType}", type);
            return;
        }

        try
        {
            using var client = new SmtpClient(_options.Smtp.Host, _options.Smtp.Port)
            {
                EnableSsl = _options.Smtp.UseSsl
            };

            if (!string.IsNullOrWhiteSpace(_options.Smtp.Username))
            {
                client.Credentials = new NetworkCredential(_options.Smtp.Username, _options.Smtp.Password);
            }

            var message = new MailMessage
            {
                From = new MailAddress(_options.Smtp.From),
                Subject = $"{type} report",
                Body = $"{type} report generated at {DateTimeOffset.UtcNow:u}",
                IsBodyHtml = false
            };

            message.To.Add(new MailAddress(_options.Smtp.From));
            message.Attachments.Add(new Attachment(filePath));

            await client.SendMailAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send email for report {ReportType}", type);
        }
    }

    private async Task<IReadOnlyList<VarianceRow>> BuildVarianceRowsAsync(CancellationToken cancellationToken)
    {
        var rows = await _dbContext.CycleCountLines
            .AsNoTracking()
            .Include(x => x.Item)
            .Include(x => x.Location)
            .Where(x => Math.Abs(x.Delta) > 0.01m)
            .ToListAsync(cancellationToken);

        return rows.Select(x => new VarianceRow(
            x.Location?.Code ?? x.LocationId.ToString(),
            x.ItemId,
            x.Item?.InternalSKU ?? string.Empty,
            x.Delta,
            x.PhysicalQty,
            x.SystemQty,
            x.CountedAt ?? DateTimeOffset.UtcNow)).ToList();
    }

    private static string Escape(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace(",", " ");

    private sealed record VarianceRow(
        string Location,
        int ItemId,
        string Sku,
        decimal Delta,
        decimal PhysicalQty,
        decimal SystemQty,
        DateTimeOffset CountedAt);
}

internal static class SimplePdfBuilder
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
        WriteLine(stream, xrefPosition.ToString(CultureInfo.InvariantCulture));
        WriteLine(stream, "%%EOF");

        return stream.ToArray();
    }
}
