using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CsvHelper;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using Marten;
using Marten.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Renci.SshNet;

namespace LKvitai.MES.Api.Services;

public sealed class TransactionExportOptions
{
    public string ExportRootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "exports", "compliance");
    public long MaxFileSizeBytes { get; set; } = 500L * 1024 * 1024;
    public int DefaultHistoryLimit { get; set; } = 100;
}

public sealed record TransactionExportSftpDestination(
    string Host,
    string Username,
    string Password,
    string? RemotePath,
    bool DeleteLocalAfterUpload);

public sealed record TransactionExportExecuteCommand(
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    TransactionExportFormat Format,
    string ExportedBy,
    TransactionExportSftpDestination? SftpDestination);

public sealed record TransactionExportExecutionResult(
    bool Succeeded,
    Guid ExportId,
    int RowCount,
    IReadOnlyList<string> FilePaths,
    TransactionExportStatus Status,
    string? ErrorMessage);

public sealed record TransactionExportHistoryDto(
    Guid ExportId,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    TransactionExportFormat Format,
    int RowCount,
    IReadOnlyList<string> FilePaths,
    TransactionExportStatus Status,
    string? ErrorMessage,
    string ExportedBy,
    DateTimeOffset ExportedAt);

public sealed record TransactionEventExportRow(
    Guid EventId,
    string EventType,
    DateTimeOffset Timestamp,
    string AggregateId,
    string? UserId,
    string PayloadJson);

public interface ITransactionEventReader
{
    Task<IReadOnlyList<TransactionEventExportRow>> ReadAsync(
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken = default);
}

public sealed class MartenTransactionEventReader : ITransactionEventReader
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IDocumentStore _documentStore;

    public MartenTransactionEventReader(IDocumentStore documentStore)
    {
        _documentStore = documentStore;
    }

    public async Task<IReadOnlyList<TransactionEventExportRow>> ReadAsync(
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken = default)
    {
        await using var querySession = _documentStore.QuerySession();

        var rawEvents = await Marten.QueryableExtensions.ToListAsync(
            querySession.Events
                .QueryAllRawEvents()
                .Where(x => x.Timestamp >= startDate && x.Timestamp <= endDate)
                .OrderBy(x => x.Timestamp),
            cancellationToken);

        return rawEvents.Select(Map).ToList();
    }

    private static TransactionEventExportRow Map(IEvent rawEvent)
    {
        var payloadJson = rawEvent.Data is null
            ? "{}"
            : JsonSerializer.Serialize(rawEvent.Data, rawEvent.Data.GetType(), PayloadJsonOptions);

        var aggregateId = string.IsNullOrWhiteSpace(rawEvent.StreamKey)
            ? rawEvent.StreamId.ToString()
            : rawEvent.StreamKey!;

        return new TransactionEventExportRow(
            rawEvent.Id,
            rawEvent.EventTypeName,
            rawEvent.Timestamp,
            aggregateId,
            TryResolveHeaderValue(rawEvent, "X-User-Id"),
            payloadJson);
    }

    private static string? TryResolveHeaderValue(IEvent rawEvent, string key)
    {
        var headersProperty = rawEvent.GetType().GetProperty("Headers");
        if (headersProperty?.GetValue(rawEvent) is not IReadOnlyDictionary<string, object?> headers)
        {
            return null;
        }

        if (headers.TryGetValue(key, out var value) && value is not null)
        {
            return value.ToString();
        }

        return null;
    }
}

public interface ITransactionExportSftpClient
{
    Task UploadAsync(
        IReadOnlyList<string> localFilePaths,
        TransactionExportSftpDestination destination,
        CancellationToken cancellationToken = default);
}

public sealed class SshNetTransactionExportSftpClient : ITransactionExportSftpClient
{
    public Task UploadAsync(
        IReadOnlyList<string> localFilePaths,
        TransactionExportSftpDestination destination,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            using var client = new SftpClient(destination.Host, destination.Username, destination.Password);
            client.Connect();

            var remotePath = string.IsNullOrWhiteSpace(destination.RemotePath)
                ? "/"
                : destination.RemotePath!.Trim();
            EnsureDirectory(client, remotePath);

            foreach (var localFilePath in localFilePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var stream = File.OpenRead(localFilePath);
                var remoteFile = CombineUnixPath(remotePath, Path.GetFileName(localFilePath));
                client.UploadFile(stream, remoteFile, true);
            }

            client.Disconnect();
        }, cancellationToken);
    }

    private static string CombineUnixPath(string basePath, string fileName)
    {
        if (string.IsNullOrWhiteSpace(basePath) || basePath == "/")
        {
            return $"/{fileName}";
        }

        return $"{basePath.TrimEnd('/')}/{fileName}";
    }

    private static void EnsureDirectory(SftpClient client, string remotePath)
    {
        var normalized = string.IsNullOrWhiteSpace(remotePath) ? "/" : remotePath.Trim();
        if (normalized == "/")
        {
            return;
        }

        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var current = "/";

        foreach (var part in parts)
        {
            current = CombineUnixPath(current, part);
            if (!client.Exists(current))
            {
                client.CreateDirectory(current);
            }
        }
    }
}

public interface ITransactionExportService
{
    Task<TransactionExportExecutionResult> ExportAsync(
        TransactionExportExecuteCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TransactionExportHistoryDto>> GetHistoryAsync(
        int? limit,
        CancellationToken cancellationToken = default);
}

public sealed class TransactionExportService : ITransactionExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly WarehouseDbContext _dbContext;
    private readonly ITransactionEventReader _eventReader;
    private readonly ITransactionExportSftpClient _sftpClient;
    private readonly TransactionExportOptions _options;
    private readonly ISecurityAuditLogService? _auditLogService;
    private readonly ILogger<TransactionExportService> _logger;

    public TransactionExportService(
        WarehouseDbContext dbContext,
        ITransactionEventReader eventReader,
        ITransactionExportSftpClient sftpClient,
        IOptions<TransactionExportOptions> options,
        ILogger<TransactionExportService> logger,
        ISecurityAuditLogService? auditLogService = null)
    {
        _dbContext = dbContext;
        _eventReader = eventReader;
        _sftpClient = sftpClient;
        _options = options.Value;
        _logger = logger;
        _auditLogService = auditLogService;
    }

    public async Task<TransactionExportExecutionResult> ExportAsync(
        TransactionExportExecuteCommand command,
        CancellationToken cancellationToken = default)
    {
        var export = new TransactionExport
        {
            StartDate = command.StartDate,
            EndDate = command.EndDate,
            Format = command.Format,
            Status = TransactionExportStatus.Pending,
            ExportedBy = string.IsNullOrWhiteSpace(command.ExportedBy) ? "system" : command.ExportedBy.Trim(),
            ExportedAt = DateTimeOffset.UtcNow
        };

        _dbContext.TransactionExports.Add(export);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (command.StartDate > command.EndDate)
        {
            export.Status = TransactionExportStatus.Failed;
            export.ErrorMessage = "StartDate must be less than or equal to EndDate.";
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new TransactionExportExecutionResult(
                false,
                export.Id,
                0,
                [],
                export.Status,
                export.ErrorMessage);
        }

        try
        {
            var rows = await _eventReader.ReadAsync(command.StartDate, command.EndDate, cancellationToken);
            var filePrefix = BuildFilePrefix();

            IReadOnlyList<string> filePaths = command.Format switch
            {
                TransactionExportFormat.Csv => await WriteCsvFilesAsync(filePrefix, rows, cancellationToken),
                TransactionExportFormat.Json => await WriteJsonFilesAsync(filePrefix, rows, cancellationToken),
                _ => throw new InvalidOperationException($"Unsupported export format '{command.Format}'.")
            };

            if (command.SftpDestination is not null)
            {
                await _sftpClient.UploadAsync(filePaths, command.SftpDestination, cancellationToken);

                if (command.SftpDestination.DeleteLocalAfterUpload)
                {
                    foreach (var filePath in filePaths)
                    {
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                    }
                }
            }

            export.RowCount = rows.Count;
            export.FilePath = string.Join(';', filePaths);
            export.Status = TransactionExportStatus.Completed;
            export.ErrorMessage = null;

            await _dbContext.SaveChangesAsync(cancellationToken);
            await WriteAuditAsync(export, "EXPORT_TRANSACTIONS", cancellationToken);

            return new TransactionExportExecutionResult(
                true,
                export.Id,
                rows.Count,
                filePaths,
                export.Status,
                null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transaction export failed: ExportId={ExportId}", export.Id);

            export.Status = TransactionExportStatus.Failed;
            export.ErrorMessage = ex.Message;
            await _dbContext.SaveChangesAsync(cancellationToken);
            await WriteAuditAsync(export, "EXPORT_TRANSACTIONS_FAILED", cancellationToken, ex.Message);

            return new TransactionExportExecutionResult(
                false,
                export.Id,
                export.RowCount,
                [],
                export.Status,
                export.ErrorMessage);
        }
    }

    public async Task<IReadOnlyList<TransactionExportHistoryDto>> GetHistoryAsync(
        int? limit,
        CancellationToken cancellationToken = default)
    {
        var take = limit is > 0 and <= 1000
            ? limit.Value
            : Math.Clamp(_options.DefaultHistoryLimit, 1, 1000);

        var query = _dbContext.TransactionExports
            .AsNoTracking()
            .OrderByDescending(x => x.ExportedAt)
            .Take(take);

        var rows = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(query, cancellationToken);

        return rows.Select(row => new TransactionExportHistoryDto(
                row.Id,
                row.StartDate,
                row.EndDate,
                row.Format,
                row.RowCount,
                SplitFilePaths(row.FilePath),
                row.Status,
                row.ErrorMessage,
                row.ExportedBy,
                row.ExportedAt))
            .ToList();
    }

    private async Task<IReadOnlyList<string>> WriteCsvFilesAsync(
        string filePrefix,
        IReadOnlyList<TransactionEventExportRow> rows,
        CancellationToken cancellationToken)
    {
        var chunks = SplitForCsv(rows);
        var root = EnsureExportDirectory();
        var filePaths = new List<string>(chunks.Count);

        for (var i = 0; i < chunks.Count; i++)
        {
            var fileName = chunks.Count == 1
                ? $"{filePrefix}.csv"
                : $"{filePrefix}-part{i + 1}.csv";
            var fullPath = Path.Combine(root, fileName);

            await using var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await using var streamWriter = new StreamWriter(stream, new UTF8Encoding(false));
            await using var csvWriter = new CsvWriter(streamWriter, CultureInfo.InvariantCulture);

            csvWriter.WriteHeader<CsvTransactionEventExportRow>();
            await csvWriter.NextRecordAsync();

            foreach (var row in chunks[i])
            {
                cancellationToken.ThrowIfCancellationRequested();
                csvWriter.WriteRecord(MapCsv(row));
                await csvWriter.NextRecordAsync();
            }

            await streamWriter.FlushAsync(cancellationToken);
            filePaths.Add(fullPath);
        }

        return filePaths;
    }

    private async Task<IReadOnlyList<string>> WriteJsonFilesAsync(
        string filePrefix,
        IReadOnlyList<TransactionEventExportRow> rows,
        CancellationToken cancellationToken)
    {
        var chunks = SplitForJson(rows);
        var root = EnsureExportDirectory();
        var filePaths = new List<string>(chunks.Count);

        for (var i = 0; i < chunks.Count; i++)
        {
            var fileName = chunks.Count == 1
                ? $"{filePrefix}.json"
                : $"{filePrefix}-part{i + 1}.json";
            var fullPath = Path.Combine(root, fileName);
            var content = JsonSerializer.Serialize(chunks[i], JsonOptions);

            await File.WriteAllTextAsync(fullPath, content, Encoding.UTF8, cancellationToken);
            filePaths.Add(fullPath);
        }

        return filePaths;
    }

    private List<List<TransactionEventExportRow>> SplitForCsv(IReadOnlyList<TransactionEventExportRow> rows)
    {
        var maxBytes = ResolveMaxFileSizeBytes();
        var chunks = new List<List<TransactionEventExportRow>>();
        var current = new List<TransactionEventExportRow>();
        var currentBytes = EstimateCsvHeaderBytes();

        foreach (var row in rows)
        {
            var rowBytes = EstimateCsvRowBytes(row);
            if (current.Count > 0 && currentBytes + rowBytes > maxBytes)
            {
                chunks.Add(current);
                current = [];
                currentBytes = EstimateCsvHeaderBytes();
            }

            current.Add(row);
            currentBytes += rowBytes;
        }

        chunks.Add(current);
        return chunks;
    }

    private List<List<TransactionEventExportRow>> SplitForJson(IReadOnlyList<TransactionEventExportRow> rows)
    {
        var maxBytes = ResolveMaxFileSizeBytes();
        var chunks = new List<List<TransactionEventExportRow>>();
        var current = new List<TransactionEventExportRow>();
        var currentBytes = 2L;

        foreach (var row in rows)
        {
            var rowBytes = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(row, JsonOptions));
            var separatorBytes = current.Count == 0 ? 0 : 1;

            if (current.Count > 0 && currentBytes + separatorBytes + rowBytes > maxBytes)
            {
                chunks.Add(current);
                current = [];
                currentBytes = 2L;
                separatorBytes = 0;
            }

            current.Add(row);
            currentBytes += separatorBytes + rowBytes;
        }

        chunks.Add(current);
        return chunks;
    }

    private long ResolveMaxFileSizeBytes()
    {
        if (_options.MaxFileSizeBytes < 1024)
        {
            return 1024;
        }

        return _options.MaxFileSizeBytes;
    }

    private static long EstimateCsvHeaderBytes()
    {
        using var stringWriter = new StringWriter(CultureInfo.InvariantCulture);
        using var csvWriter = new CsvWriter(stringWriter, CultureInfo.InvariantCulture);
        csvWriter.WriteHeader<CsvTransactionEventExportRow>();
        csvWriter.NextRecord();
        return Encoding.UTF8.GetByteCount(stringWriter.ToString());
    }

    private static long EstimateCsvRowBytes(TransactionEventExportRow row)
    {
        using var stringWriter = new StringWriter(CultureInfo.InvariantCulture);
        using var csvWriter = new CsvWriter(stringWriter, CultureInfo.InvariantCulture);
        csvWriter.WriteRecord(MapCsv(row));
        csvWriter.NextRecord();
        return Encoding.UTF8.GetByteCount(stringWriter.ToString());
    }

    private string EnsureExportDirectory()
    {
        var configured = string.IsNullOrWhiteSpace(_options.ExportRootPath)
            ? Path.Combine(AppContext.BaseDirectory, "exports", "compliance")
            : _options.ExportRootPath;

        Directory.CreateDirectory(configured);
        return configured;
    }

    private static string BuildFilePrefix()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
        return $"transactions-{timestamp}-{nonce}";
    }

    private async Task WriteAuditAsync(
        TransactionExport export,
        string action,
        CancellationToken cancellationToken,
        string? errorMessage = null)
    {
        if (_auditLogService is null)
        {
            return;
        }

        var details = JsonSerializer.Serialize(new
        {
            exportId = export.Id,
            format = export.Format.ToString(),
            rowCount = export.RowCount,
            filePath = export.FilePath,
            status = export.Status.ToString(),
            errorMessage
        });

        await _auditLogService.WriteAsync(
            new SecurityAuditLogWriteRequest(
                export.ExportedBy,
                action,
                "TRANSACTION_EXPORT",
                export.Id.ToString(),
                "system",
                "transaction-export-service",
                DateTimeOffset.UtcNow,
                details),
            cancellationToken);
    }

    private static IReadOnlyList<string> SplitFilePaths(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return [];
        }

        return filePath
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static CsvTransactionEventExportRow MapCsv(TransactionEventExportRow row)
    {
        return new CsvTransactionEventExportRow(
            row.EventId,
            row.EventType,
            row.Timestamp,
            row.AggregateId,
            row.UserId,
            row.PayloadJson);
    }

    private sealed record CsvTransactionEventExportRow(
        Guid EventId,
        string EventType,
        DateTimeOffset Timestamp,
        string AggregateId,
        string? UserId,
        string Payload);
}
