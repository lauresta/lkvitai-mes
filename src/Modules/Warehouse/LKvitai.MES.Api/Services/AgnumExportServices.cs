using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using CsvHelper;
using Hangfire;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.Messages;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using Marten;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;

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
    private static readonly Meter AgnumMeter = new("LKvitai.MES.Integration.Agnum");
    private static readonly Histogram<double> CsvGenerationDurationMs =
        AgnumMeter.CreateHistogram<double>("agnum_csv_generation_duration_ms");
    private static readonly Counter<long> ApiCallsTotal =
        AgnumMeter.CreateCounter<long>("agnum_api_calls_total");
    private static readonly Histogram<double> ApiLatencyMs =
        AgnumMeter.CreateHistogram<double>("agnum_api_latency_ms");
    private static readonly JsonSerializerOptions ApiJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private const string ApiImportPath = "api/v1/inventory/import";
    private const int MaxPayloadBytesBeforeCompression = 10 * 1024 * 1024;

    private readonly WarehouseDbContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAgnumSecretProtector _secretProtector;
    private readonly ILogger<AgnumExportOrchestrator> _logger;
    private readonly string _exportRootPath;
    private readonly IDocumentStore? _documentStore;

    public AgnumExportOrchestrator(
        WarehouseDbContext dbContext,
        IEventBus eventBus,
        IHttpClientFactory httpClientFactory,
        IAgnumSecretProtector secretProtector,
        IConfiguration configuration,
        ILogger<AgnumExportOrchestrator> logger,
        IDocumentStore? documentStore = null)
    {
        _dbContext = dbContext;
        _eventBus = eventBus;
        _httpClientFactory = httpClientFactory;
        _secretProtector = secretProtector;
        _logger = logger;
        _documentStore = documentStore;
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

            var exportRows = await LoadExportRowsAsync(config, cancellationToken);

            var csvPayload = BuildCsv(exportRows);

            var filePath = await WritePayloadAsync(
                history.ExportNumber,
                AgnumExportFormat.Csv,
                csvPayload,
                cancellationToken);

            history.RowCount = exportRows.Count;
            history.FilePath = filePath;
            history.ErrorMessage = null;

            _logger.LogInformation(
                "CSV generated: {RowCount} rows, FilePath {FilePath}",
                exportRows.Count,
                filePath);

            if (config.Format == AgnumExportFormat.JsonApi &&
                !string.IsNullOrWhiteSpace(config.ApiEndpoint))
            {
                await SendToAgnumApiAsync(
                    config,
                    history.ExportNumber,
                    exportRows,
                    cancellationToken);
            }

            history.Status = AgnumExportStatus.Success;

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

    private async Task<IReadOnlyList<AgnumExportRow>> LoadExportRowsAsync(
        AgnumExportConfig config,
        CancellationToken cancellationToken)
    {
        var utcDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var onHandRows = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            _dbContext.OnHandValues.AsNoTracking().OrderBy(x => x.ItemSku),
            cancellationToken);

        if (_documentStore is null)
        {
            return onHandRows.Select(x => new AgnumExportRow(
                    utcDate,
                    ResolveAccountCode(config, x),
                    x.ItemSku,
                    x.ItemName,
                    x.Qty,
                    x.UnitCost,
                    x.TotalValue))
                .ToList();
        }

        try
        {
            await using var session = _documentStore.QuerySession();
            var stockRows = await Marten.QueryableExtensions.ToListAsync(
                session.Query<AvailableStockView>().Where(x => x.OnHandQty > 0m),
                cancellationToken);

            if (stockRows.Count == 0)
            {
                return onHandRows.Select(x => new AgnumExportRow(
                        utcDate,
                        ResolveAccountCode(config, x),
                        x.ItemSku,
                        x.ItemName,
                        x.Qty,
                        x.UnitCost,
                        x.TotalValue))
                    .ToList();
            }

            var itemSkus = stockRows
                .Select(x => x.SKU)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var itemMap = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToDictionaryAsync(
                _dbContext.Items
                    .AsNoTracking()
                    .Include(x => x.Category)
                    .Where(x => itemSkus.Contains(x.InternalSKU))
                    .OrderBy(x => x.InternalSKU),
                x => x.InternalSKU,
                x => x,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);

            var onHandBySku = onHandRows.ToDictionary(
                x => x.ItemSku,
                x => x,
                StringComparer.OrdinalIgnoreCase);

            return stockRows
                .GroupBy(x => x.SKU, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var sku = group.Key;
                    var qty = group.Sum(x => x.OnHandQty);

                    onHandBySku.TryGetValue(sku, out var valuation);
                    itemMap.TryGetValue(sku, out var item);

                    var unitCost = valuation?.UnitCost ?? 0m;
                    var totalValue = decimal.Round(qty * unitCost, 4, MidpointRounding.AwayFromZero);
                    var categoryId = item?.CategoryId ?? valuation?.CategoryId;
                    var categoryName = item?.Category?.Name ?? valuation?.CategoryName;

                    var mappingSource = new OnHandValue
                    {
                        ItemId = item?.Id ?? valuation?.ItemId ?? 0,
                        ItemSku = sku,
                        ItemName = item?.Name ?? valuation?.ItemName ?? sku,
                        CategoryId = categoryId,
                        CategoryName = categoryName,
                        Qty = qty,
                        UnitCost = unitCost,
                        TotalValue = totalValue
                    };

                    return new AgnumExportRow(
                        utcDate,
                        ResolveAccountCode(config, mappingSource),
                        sku,
                        mappingSource.ItemName,
                        qty,
                        unitCost,
                        totalValue);
                })
                .OrderBy(x => x.Sku, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falling back to on_hand_value export source after AvailableStock query failure.");
            return onHandRows.Select(x => new AgnumExportRow(
                    utcDate,
                    ResolveAccountCode(config, x),
                    x.ItemSku,
                    x.ItemName,
                    x.Qty,
                    x.UnitCost,
                    x.TotalValue))
                .ToList();
        }
    }

    private async Task SendToAgnumApiAsync(
        AgnumExportConfig config,
        string exportNumber,
        IReadOnlyCollection<AgnumExportRow> rows,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("AgnumExportApi");
        var apiKey = _secretProtector.Unprotect(config.ApiKey);

        var endpoint = BuildImportEndpoint(config.ApiEndpoint!);
        var payloads = BuildApiPayloads(rows).ToList();

        var retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<InvalidOperationException>(),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = false,
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        args.Outcome.Exception,
                        "Agnum API call retry {Attempt}/3 after {DelaySeconds}s",
                        args.AttemptNumber + 1,
                        args.RetryDelay.TotalSeconds);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();

        foreach (var payload in payloads)
        {
            var payloadJson = JsonSerializer.Serialize(payload, ApiJsonOptions);
            var startedAt = Stopwatch.GetTimestamp();

            try
            {
                await retryPipeline.ExecuteAsync(async token =>
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                    {
                        Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
                    };
                    request.Headers.Add("X-Export-ID", exportNumber);

                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        request.Headers.Add("Authorization", $"Bearer {apiKey}");
                    }

                    using var response = await client.SendAsync(request, token);
                    if (!response.IsSuccessStatusCode)
                    {
                        var responseBody = await response.Content.ReadAsStringAsync(token);
                        throw new InvalidOperationException(
                            $"Agnum API request failed with status {(int)response.StatusCode}: {responseBody}");
                    }
                }, cancellationToken);

                ApiCallsTotal.Add(1, new KeyValuePair<string, object?>("status", "success"));
                ApiLatencyMs.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

                _logger.LogInformation(
                    "Agnum API call succeeded: ExportId {ExportId}, AccountCode {AccountCode}",
                    exportNumber,
                    payload.AccountCode);
            }
            catch (Exception)
            {
                ApiCallsTotal.Add(1, new KeyValuePair<string, object?>("status", "error"));
                ApiLatencyMs.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                throw;
            }
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

        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        if (payloadBytes.Length > MaxPayloadBytesBeforeCompression)
        {
            var compressedFileName = $"{exportNumber}.{extension}.gz";
            var compressedPath = Path.Combine(directory, compressedFileName);

            await using var fileStream = File.Create(compressedPath);
            await using var gzipStream = new GZipStream(fileStream, CompressionLevel.SmallestSize);
            await gzipStream.WriteAsync(payloadBytes, cancellationToken);

            _logger.LogInformation(
                "Agnum payload compressed: ExportNumber {ExportNumber}, OriginalBytes {OriginalBytes}, FilePath {FilePath}",
                exportNumber,
                payloadBytes.Length,
                compressedPath);

            return compressedPath;
        }

        var fileName = $"{exportNumber}.{extension}";
        var filePath = Path.Combine(directory, fileName);
        await File.WriteAllBytesAsync(filePath, payloadBytes, cancellationToken);
        return filePath;
    }

    private static string BuildCsv(IReadOnlyCollection<AgnumExportRow> rows)
    {
        var startedAt = Stopwatch.GetTimestamp();
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteRecords(rows.Select(x => new AgnumCsvRow(
            x.ExportDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            x.AccountCode,
            x.Sku,
            x.ItemName,
            x.Quantity,
            x.UnitCost,
            x.OnHandValue)));
        CsvGenerationDurationMs.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        return writer.ToString();
    }

    private static string BuildImportEndpoint(string apiEndpoint)
    {
        var baseUri = new Uri(apiEndpoint.EndsWith("/", StringComparison.Ordinal)
            ? apiEndpoint
            : $"{apiEndpoint}/");
        return new Uri(baseUri, ApiImportPath).ToString();
    }

    private static IEnumerable<AgnumApiPayload> BuildApiPayloads(IReadOnlyCollection<AgnumExportRow> rows)
    {
        return rows
            .GroupBy(x => x.AccountCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AgnumApiPayload(
                group.First().ExportDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                group.Key,
                group.Select(x => new AgnumApiItemPayload(
                    x.Sku,
                    x.ItemName,
                    x.Quantity,
                    x.UnitCost,
                    x.OnHandValue)).ToList()));
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

    private sealed record AgnumCsvRow(
        string ExportDate,
        string AccountCode,
        string SKU,
        string ItemName,
        decimal Quantity,
        decimal UnitCost,
        decimal OnHandValue);

    private sealed record AgnumApiPayload(
        string ExportDate,
        string AccountCode,
        IReadOnlyCollection<AgnumApiItemPayload> Items);

    private sealed record AgnumApiItemPayload(
        string Sku,
        string Name,
        decimal Qty,
        decimal Cost,
        decimal Value);
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
