using System.Collections.Concurrent;
using System.Text.Json;

namespace LKvitai.MES.Api.Services;

public enum PrintQueueStatus
{
    Pending,
    Printing,
    Completed,
    Failed
}

public sealed class PrintQueueItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string TemplateType { get; init; } = string.Empty;
    public string DataJson { get; init; } = "{}";
    public PrintQueueStatus Status { get; set; } = PrintQueueStatus.Pending;
    public int RetryCount { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastAttemptAt { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

public interface ILabelPrintQueueStore
{
    PrintQueueItem Enqueue(string templateType, IReadOnlyDictionary<string, string> data, string? errorMessage = null);
    IReadOnlyList<PrintQueueItem> List(Func<PrintQueueItem, bool> predicate);
    PrintQueueItem? Get(Guid id);
    void Save(PrintQueueItem item);
}

public sealed class InMemoryLabelPrintQueueStore : ILabelPrintQueueStore
{
    private readonly ConcurrentDictionary<Guid, PrintQueueItem> _items = new();

    public PrintQueueItem Enqueue(string templateType, IReadOnlyDictionary<string, string> data, string? errorMessage = null)
    {
        var item = new PrintQueueItem
        {
            Id = Guid.NewGuid(),
            TemplateType = templateType,
            DataJson = JsonSerializer.Serialize(data),
            Status = PrintQueueStatus.Pending,
            RetryCount = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            ErrorMessage = errorMessage ?? string.Empty
        };

        _items[item.Id] = item;
        return item;
    }

    public IReadOnlyList<PrintQueueItem> List(Func<PrintQueueItem, bool> predicate)
    {
        return _items.Values
            .Where(predicate)
            .OrderBy(x => x.CreatedAt)
            .ToList();
    }

    public PrintQueueItem? Get(Guid id)
    {
        return _items.TryGetValue(id, out var item) ? item : null;
    }

    public void Save(PrintQueueItem item)
    {
        _items[item.Id] = item;
    }
}

public sealed record PrintQueueRetryResult(
    bool Found,
    PrintQueueItem? Item,
    string? Error = null);

public interface ILabelPrintQueueProcessor
{
    Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default);
    Task<PrintQueueRetryResult> RetryNowAsync(Guid id, CancellationToken cancellationToken = default);
    IReadOnlyList<PrintQueueItem> GetPendingAndFailed();
}

public sealed class LabelPrintQueueProcessor : ILabelPrintQueueProcessor
{
    private const int MaxQueueRetries = 10;

    private readonly ILabelPrintQueueStore _store;
    private readonly ILabelPrinterClient _printerClient;
    private readonly LabelTemplateEngine _templateEngine;
    private readonly ILogger<LabelPrintQueueProcessor> _logger;

    public LabelPrintQueueProcessor(
        ILabelPrintQueueStore store,
        ILabelPrinterClient printerClient,
        LabelTemplateEngine templateEngine,
        ILogger<LabelPrintQueueProcessor> logger)
    {
        _store = store;
        _printerClient = printerClient;
        _templateEngine = templateEngine;
        _logger = logger;
    }

    public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default)
    {
        var processed = 0;
        var pending = _store.List(x => x.Status == PrintQueueStatus.Pending);
        foreach (var item in pending)
        {
            processed++;
            await AttemptPrintAsync(item, cancellationToken);
        }

        return processed;
    }

    public async Task<PrintQueueRetryResult> RetryNowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = _store.Get(id);
        if (item is null)
        {
            return new PrintQueueRetryResult(false, null, "Queue item not found.");
        }

        await AttemptPrintAsync(item, cancellationToken);
        return new PrintQueueRetryResult(true, item);
    }

    public IReadOnlyList<PrintQueueItem> GetPendingAndFailed()
    {
        return _store.List(x => x.Status is PrintQueueStatus.Pending or PrintQueueStatus.Failed);
    }

    private async Task AttemptPrintAsync(PrintQueueItem item, CancellationToken cancellationToken)
    {
        item.Status = PrintQueueStatus.Printing;
        item.LastAttemptAt = DateTimeOffset.UtcNow;
        _store.Save(item);

        try
        {
            var data = DeserializeData(item.DataJson);
            var zpl = _templateEngine.Render(item.TemplateType, data);
            await _printerClient.SendAsync(zpl, cancellationToken);

            item.Status = PrintQueueStatus.Completed;
            item.ErrorMessage = string.Empty;
            item.LastAttemptAt = DateTimeOffset.UtcNow;
            _store.Save(item);
        }
        catch (Exception ex)
        {
            item.RetryCount++;
            item.ErrorMessage = ex.Message;
            item.LastAttemptAt = DateTimeOffset.UtcNow;
            item.Status = item.RetryCount < MaxQueueRetries
                ? PrintQueueStatus.Pending
                : PrintQueueStatus.Failed;
            _store.Save(item);

            _logger.LogWarning(
                ex,
                "Queued print attempt failed. QueueId={QueueId} RetryCount={RetryCount} Status={Status}",
                item.Id,
                item.RetryCount,
                item.Status);
        }
    }

    private static IReadOnlyDictionary<string, string> DeserializeData(string dataJson)
    {
        return JsonSerializer.Deserialize<Dictionary<string, string>>(dataJson)
               ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}

public sealed class LabelPrintQueueRecurringJob
{
    private readonly ILabelPrintQueueProcessor _processor;
    private readonly ILogger<LabelPrintQueueRecurringJob> _logger;

    public LabelPrintQueueRecurringJob(
        ILabelPrintQueueProcessor processor,
        ILogger<LabelPrintQueueRecurringJob> logger)
    {
        _processor = processor;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        var processed = await _processor.ProcessPendingAsync();
        _logger.LogInformation("Processed {Count} queued print jobs.", processed);
    }
}
