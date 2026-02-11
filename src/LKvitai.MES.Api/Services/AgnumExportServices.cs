using System.Text;
using System.Text.Json;
using Hangfire;
using LKvitai.MES.Application.Ports;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.Messages;
using LKvitai.MES.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Api.Services;

public static class AgnumRecurringJobs
{
    public const string DailyExportJobId = "agnum-daily-export";
    public const int MaxRetryAttempts = 3;
}

public interface IAgnumSecretProtector
{
    string Protect(string plainText);

    string? Unprotect(string? cipherText);
}

public sealed class AgnumDataProtector : IAgnumSecretProtector
{
    private readonly IDataProtector _protector;

    public AgnumDataProtector(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("LKvitai.MES.Agnum.ApiKey.v1");
    }

    public string Protect(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            return string.Empty;
        }

        return _protector.Protect(plainText);
    }

    public string? Unprotect(string? cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(cipherText);
        }
        catch
        {
            return null;
        }
    }
}

public sealed record AgnumExportExecutionResult(
    bool IsSuccess,
    Guid? HistoryId,
    string ExportNumber,
    AgnumExportStatus Status,
    int RowCount,
    string? FilePath,
    string? ErrorMessage);

public interface IAgnumExportOrchestrator
{
    Task<AgnumExportExecutionResult> ExecuteAsync(
        string trigger,
        int retryAttempt,
        Guid? historyId = null,
        CancellationToken cancellationToken = default);
}

public sealed class AgnumExportOrchestrator : IAgnumExportOrchestrator
{
    private readonly WarehouseDbContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAgnumSecretProtector _secretProtector;
    private readonly ILogger<AgnumExportOrchestrator> _logger;
    private readonly string _exportRootPath;

    public AgnumExportOrchestrator(
        WarehouseDbContext dbContext,
        IEventBus eventBus,
        IHttpClientFactory httpClientFactory,
        IAgnumSecretProtector secretProtector,
        IConfiguration configuration,
        ILogger<AgnumExportOrchestrator> logger)
    {
        _dbContext = dbContext;
        _eventBus = eventBus;
        _httpClientFactory = httpClientFactory;
        _secretProtector = secretProtector;
        _logger = logger;
        _exportRootPath = configuration["Agnum:ExportRootPath"] ??
                          Path.Combine(AppContext.BaseDirectory, "exports", "agnum");
    }

    public async Task<AgnumExportExecutionResult> ExecuteAsync(
        string trigger,
        int retryAttempt,
        Guid? historyId = null,
        CancellationToken cancellationToken = default)
    {
        var configQuery = _dbContext.AgnumExportConfigs
            .Include(x => x.Mappings)
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.UpdatedAt);
        var config = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            configQuery,
            cancellationToken);

        if (config is null)
        {
            return new AgnumExportExecutionResult(
                false,
                historyId,
                string.Empty,
                AgnumExportStatus.Failed,
                0,
                null,
                "Active Agnum export configuration not found.");
        }

        AgnumExportHistory history;
        if (historyId.HasValue)
        {
            var historyQuery = _dbContext.AgnumExportHistories.AsQueryable();
            history = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
                          historyQuery,
                          x => x.Id == historyId.Value,
                          cancellationToken)
                      ?? CreateHistory(config.Id, trigger);
        }
        else
        {
            history = CreateHistory(config.Id, trigger);
        }

        if (_dbContext.Entry(history).State == EntityState.Detached)
        {
            _dbContext.AgnumExportHistories.Add(history);
        }

        history.RetryCount = retryAttempt;
        history.ExportedAt = DateTimeOffset.UtcNow;

        try
        {
            await _eventBus.PublishAsync(new AgnumExportStartedEvent
            {
                ExportId = history.Id,
                ExportNumber = history.ExportNumber,
                Trigger = trigger.ToUpperInvariant()
            }, cancellationToken);

            _logger.LogInformation(
                "Agnum export started: ExportNumber {ExportNumber}",
                history.ExportNumber);

            var onHandRows = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                _dbContext.OnHandValues.AsNoTracking().OrderBy(x => x.ItemSku),
                cancellationToken);

            var exportRows = onHandRows.Select(x => new AgnumExportRow(
                DateOnly.FromDateTime(DateTime.UtcNow),
                ResolveAccountCode(config, x),
                x.ItemSku,
                x.ItemName,
                x.Qty,
                x.UnitCost,
                x.TotalValue))
                .ToList();

            var payload = config.Format switch
            {
                AgnumExportFormat.JsonApi => BuildJson(exportRows, history.ExportNumber),
                _ => BuildCsv(exportRows)
            };

            var filePath = await WritePayloadAsync(
                history.ExportNumber,
                config.Format,
                payload,
                cancellationToken);

            if (config.Format == AgnumExportFormat.JsonApi &&
                !string.IsNullOrWhiteSpace(config.ApiEndpoint))
            {
                await SendToAgnumApiAsync(
                    config,
                    history.ExportNumber,
                    payload,
                    cancellationToken);
            }

            history.Status = AgnumExportStatus.Success;
            history.RowCount = exportRows.Count;
            history.FilePath = filePath;
            history.ErrorMessage = null;

            await _dbContext.SaveChangesAsync(cancellationToken);

            await _eventBus.PublishAsync(new AgnumExportCompletedEvent
            {
                ExportId = history.Id,
                ExportNumber = history.ExportNumber,
                RowCount = history.RowCount,
                FilePath = history.FilePath
            }, cancellationToken);

            _logger.LogInformation(
                "Agnum export completed: {RowCount} rows, FilePath {FilePath}",
                history.RowCount,
                history.FilePath);

            return new AgnumExportExecutionResult(
                true,
                history.Id,
                history.ExportNumber,
                history.Status,
                history.RowCount,
                history.FilePath,
                null);
        }
        catch (Exception ex)
        {
            history.Status = retryAttempt < AgnumRecurringJobs.MaxRetryAttempts
                ? AgnumExportStatus.Retrying
                : AgnumExportStatus.Failed;
            history.ErrorMessage = ex.Message;
            history.RetryCount = retryAttempt;
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _eventBus.PublishAsync(new AgnumExportFailedEvent
            {
                ExportId = history.Id,
                ExportNumber = history.ExportNumber,
                ErrorMessage = ex.Message,
                RetryCount = retryAttempt
            }, cancellationToken);

            _logger.LogError(
                ex,
                "Agnum export failed: {ExportNumber}",
                history.ExportNumber);

            return new AgnumExportExecutionResult(
                false,
                history.Id,
                history.ExportNumber,
                history.Status,
                history.RowCount,
                history.FilePath,
                ex.Message);
        }
    }

    private static string ResolveAccountCode(AgnumExportConfig config, OnHandValue row)
    {
        var mappings = config.Mappings;

        static string? Match(IEnumerable<AgnumMapping> source, string sourceType, string sourceValue)
        {
            return source
                .FirstOrDefault(x =>
                    string.Equals(x.SourceType, sourceType, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.SourceValue, sourceValue, StringComparison.OrdinalIgnoreCase))
                ?.AgnumAccountCode;
        }

        return config.Scope switch
        {
            AgnumExportScope.ByCategory =>
                Match(mappings, "CATEGORY", row.CategoryName ?? string.Empty) ??
                Match(mappings, "CATEGORY", row.CategoryId?.ToString() ?? string.Empty) ??
                Match(mappings, "CATEGORY", "DEFAULT") ??
                "UNMAPPED",
            AgnumExportScope.ByWarehouse =>
                Match(mappings, "WAREHOUSE", "DEFAULT") ?? "UNMAPPED",
            AgnumExportScope.ByLogicalWh =>
                Match(mappings, "LOGICAL_WH", "DEFAULT") ?? "UNMAPPED",
            AgnumExportScope.TotalOnly =>
                Match(mappings, "TOTAL_ONLY", "TOTAL") ??
                Match(mappings, "TOTAL", "TOTAL") ??
                "UNMAPPED",
            _ => "UNMAPPED"
        };
    }

    private async Task SendToAgnumApiAsync(
        AgnumExportConfig config,
        string exportNumber,
        string payload,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("AgnumExportApi");
        using var request = new HttpRequestMessage(HttpMethod.Post, config.ApiEndpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Export-ID", exportNumber);

        var apiKey = _secretProtector.Unprotect(config.ApiKey);
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Add("Authorization", $"Bearer {apiKey}");
        }

        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Agnum API request failed with status {(int)response.StatusCode}: {responseBody}");
        }
    }

    private async Task<string> WritePayloadAsync(
        string exportNumber,
        AgnumExportFormat format,
        string payload,
        CancellationToken cancellationToken)
    {
        var dateFolder = DateTime.UtcNow.ToString("yyyyMMdd");
        var extension = format == AgnumExportFormat.JsonApi ? "json" : "csv";
        var directory = Path.Combine(_exportRootPath, dateFolder);
        Directory.CreateDirectory(directory);

        var fileName = $"{exportNumber}.{extension}";
        var filePath = Path.Combine(directory, fileName);
        await File.WriteAllTextAsync(filePath, payload, cancellationToken);
        return filePath;
    }

    private static string BuildCsv(IReadOnlyCollection<AgnumExportRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ExportDate,AccountCode,SKU,ItemName,Quantity,UnitCost,OnHandValue");
        foreach (var row in rows)
        {
            sb.Append(row.ExportDate.ToString("yyyy-MM-dd")).Append(',');
            sb.Append(EscapeCsv(row.AccountCode)).Append(',');
            sb.Append(EscapeCsv(row.Sku)).Append(',');
            sb.Append(EscapeCsv(row.ItemName)).Append(',');
            sb.Append(row.Quantity.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(',');
            sb.Append(row.UnitCost.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)).Append(',');
            sb.Append(row.OnHandValue.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string BuildJson(IReadOnlyCollection<AgnumExportRow> rows, string exportNumber)
    {
        var payload = new
        {
            exportNumber,
            exportedAt = DateTime.UtcNow,
            rows
        };
        return JsonSerializer.Serialize(payload);
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    private static AgnumExportHistory CreateHistory(Guid configId, string trigger)
    {
        var exportNumber = $"AGX-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        return new AgnumExportHistory
        {
            Id = Guid.NewGuid(),
            ExportConfigId = configId,
            ExportNumber = exportNumber,
            ExportedAt = DateTimeOffset.UtcNow,
            Status = AgnumExportStatus.Retrying,
            RetryCount = 0,
            Trigger = trigger.ToUpperInvariant()
        };
    }

    private sealed record AgnumExportRow(
        DateOnly ExportDate,
        string AccountCode,
        string Sku,
        string ItemName,
        decimal Quantity,
        decimal UnitCost,
        decimal OnHandValue);
}

public sealed class AgnumExportRecurringJob
{
    private readonly IAgnumExportOrchestrator _orchestrator;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IEventBus _eventBus;
    private readonly ILogger<AgnumExportRecurringJob> _logger;

    public AgnumExportRecurringJob(
        IAgnumExportOrchestrator orchestrator,
        IBackgroundJobClient backgroundJobClient,
        IEventBus eventBus,
        ILogger<AgnumExportRecurringJob> logger)
    {
        _orchestrator = orchestrator;
        _backgroundJobClient = backgroundJobClient;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<AgnumExportExecutionResult> ExecuteAsync(
        string trigger = "SCHEDULED",
        Guid? historyId = null,
        int retryAttempt = 0)
    {
        var correlationId = historyId ?? Guid.NewGuid();
        await _eventBus.PublishAsync(new StartAgnumExport
        {
            CorrelationId = correlationId,
            Trigger = trigger,
            RetryCount = retryAttempt
        });

        var result = await _orchestrator.ExecuteAsync(
            trigger,
            retryAttempt,
            historyId,
            CancellationToken.None);

        if (result.IsSuccess)
        {
            await _eventBus.PublishAsync(new AgnumExportSucceeded
            {
                CorrelationId = result.HistoryId ?? correlationId,
                ExportNumber = result.ExportNumber,
                RowCount = result.RowCount,
                FilePath = result.FilePath
            });
        }
        else
        {
            await _eventBus.PublishAsync(new AgnumExportFailed
            {
                CorrelationId = result.HistoryId ?? correlationId,
                ExportNumber = result.ExportNumber,
                ErrorMessage = result.ErrorMessage ?? "Unknown error",
                RetryCount = retryAttempt
            });
        }

        if (result.IsSuccess ||
            result.Status != AgnumExportStatus.Retrying ||
            retryAttempt >= AgnumRecurringJobs.MaxRetryAttempts ||
            !result.HistoryId.HasValue)
        {
            return result;
        }

        var nextRetryAttempt = retryAttempt + 1;
        var delay = TimeSpan.FromHours(Math.Pow(2, retryAttempt));

        _backgroundJobClient.Schedule<AgnumExportRecurringJob>(
            x => x.ExecuteAsync(trigger, result.HistoryId, nextRetryAttempt),
            delay);

        _logger.LogWarning(
            "Agnum export retry {RetryAttempt}/{MaxAttempts} scheduled after {DelayHours} hour(s): {ExportNumber}",
            nextRetryAttempt,
            AgnumRecurringJobs.MaxRetryAttempts,
            delay.TotalHours,
            result.ExportNumber);

        return result;
    }
}
