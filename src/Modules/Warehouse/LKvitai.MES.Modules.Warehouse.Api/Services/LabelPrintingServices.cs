using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using Hangfire;
using Microsoft.Extensions.Options;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public interface ILabelPrintOrchestrator
{
    Task<LabelPrintResult> PrintAsync(
        string labelType,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken = default);

    Task<LabelPreviewResult> GeneratePreviewAsync(
        string labelType,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken = default);

    Task<LabelPdfFileResult?> GetPdfAsync(
        string fileName,
        CancellationToken cancellationToken = default);

    Task ProcessQueuedAsync(LabelPrintJobPayload payload, int attempt);
}

public sealed record LabelPrintResult(
    string Status,
    string? PdfUrl = null,
    string? Message = null);

public sealed record LabelPreviewResult(
    byte[] Content,
    string ContentType,
    string FileName);

public sealed record LabelPdfFileResult(
    byte[] Content,
    string ContentType,
    string FileName);

public sealed record LabelPrintJobPayload(
    Guid RequestId,
    string LabelType,
    Dictionary<string, string> Data);

public sealed class LabelPrintingConfig
{
    public string PrinterIP { get; set; } = "127.0.0.1";
    public int PrinterPort { get; set; } = 9100;
    public int RetryCount { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 1000;
    public int SocketTimeoutMs { get; set; } = 5000;
}

public sealed class LabelPrinterUnavailableException : Exception
{
    public LabelPrinterUnavailableException(int attempts, Exception innerException)
        : base($"Printer offline after {attempts} retries.", innerException)
    {
        Attempts = attempts;
    }

    public int Attempts { get; }
}

public interface ILabelPrinterClient
{
    Task SendAsync(string zplPayload, CancellationToken cancellationToken = default);
}

public interface ILabelPrinterTransport
{
    Task SendAsync(
        string host,
        int port,
        string zplPayload,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

public sealed class TcpLabelPrinterTransport : ILabelPrinterTransport
{
    public async Task SendAsync(
        string host,
        int port,
        string zplPayload,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        using var client = new TcpClient();
        await client.ConnectAsync(host, port, timeoutCts.Token);

        await using var stream = client.GetStream();
        var bytes = Encoding.UTF8.GetBytes(zplPayload);
        await stream.WriteAsync(bytes, timeoutCts.Token);
        await stream.FlushAsync(timeoutCts.Token);
    }
}

public sealed class TcpLabelPrinterClient : ILabelPrinterClient
{
    private readonly LabelPrintingConfig _config;
    private readonly ILabelPrinterTransport _transport;
    private readonly ILogger<TcpLabelPrinterClient> _logger;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

    public TcpLabelPrinterClient(
        IOptions<LabelPrintingConfig> options,
        ILabelPrinterTransport transport,
        ILogger<TcpLabelPrinterClient> logger)
        : this(options, transport, logger, static (delay, ct) => Task.Delay(delay, ct))
    {
    }

    internal TcpLabelPrinterClient(
        IOptions<LabelPrintingConfig> options,
        ILabelPrinterTransport transport,
        ILogger<TcpLabelPrinterClient> logger,
        Func<TimeSpan, CancellationToken, Task> delayAsync)
    {
        _config = options.Value;
        _transport = transport;
        _logger = logger;
        _delayAsync = delayAsync;
    }

    public async Task SendAsync(string zplPayload, CancellationToken cancellationToken = default)
    {
        var host = string.IsNullOrWhiteSpace(_config.PrinterIP) ? "127.0.0.1" : _config.PrinterIP.Trim();
        var port = _config.PrinterPort > 0 ? _config.PrinterPort : 9100;
        var timeout = TimeSpan.FromMilliseconds(_config.SocketTimeoutMs <= 0 ? 5000 : _config.SocketTimeoutMs);
        var retryCount = Math.Max(1, _config.RetryCount);
        var retryDelay = TimeSpan.FromMilliseconds(_config.RetryDelayMs < 0 ? 0 : _config.RetryDelayMs);

        Exception? lastException = null;
        for (var attempt = 1; attempt <= retryCount; attempt++)
        {
            try
            {
                await _transport.SendAsync(host, port, zplPayload, timeout, cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < retryCount)
            {
                lastException = ex;
                _logger.LogWarning(
                    ex,
                    "Label printer attempt {Attempt}/{RetryCount} failed for {Host}:{Port}. Retrying in {DelayMs}ms.",
                    attempt,
                    retryCount,
                    host,
                    port,
                    retryDelay.TotalMilliseconds);

                await _delayAsync(retryDelay, cancellationToken);
            }
            catch (Exception ex)
            {
                lastException = ex;
                break;
            }
        }

        throw new LabelPrinterUnavailableException(
            retryCount,
            lastException ?? new InvalidOperationException("Unknown printer error."));
    }
}

public sealed class LabelPrintOrchestrator : ILabelPrintOrchestrator
{
    private static readonly Meter Meter = new("LKvitai.MES.LabelPrinting");
    private static readonly Counter<long> LabelPrintsTotal =
        Meter.CreateCounter<long>("label_prints_total");
    private static readonly Histogram<double> LabelPrintDurationMs =
        Meter.CreateHistogram<double>("label_print_duration_ms");
    private static readonly Counter<long> PrinterOfflineTotal =
        Meter.CreateCounter<long>("printer_offline_total");

    private readonly ILabelPrinterClient _printerClient;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILabelPrintQueueStore _printQueueStore;
    private readonly LabelTemplateEngine _templateEngine;
    private readonly ILogger<LabelPrintOrchestrator> _logger;
    private readonly string _outputRootPath;

    public LabelPrintOrchestrator(
        ILabelPrinterClient printerClient,
        IBackgroundJobClient backgroundJobs,
        ILabelPrintQueueStore printQueueStore,
        LabelTemplateEngine templateEngine,
        IConfiguration configuration,
        ILogger<LabelPrintOrchestrator> logger)
    {
        _printerClient = printerClient;
        _backgroundJobs = backgroundJobs;
        _printQueueStore = printQueueStore;
        _templateEngine = templateEngine;
        _logger = logger;
        _outputRootPath = configuration["Labels:OutputRootPath"] ??
                          Path.Combine(AppContext.BaseDirectory, "labels");
    }

    public async Task<LabelPrintResult> PrintAsync(
        string labelType,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var parsedTemplateType = _templateEngine.ParseTemplateType(labelType);
        var normalizedLabelType = _templateEngine.ToApiTemplateType(parsedTemplateType);
        var normalizedData = NormalizeData(data);
        var zpl = _templateEngine.Render(parsedTemplateType, normalizedData);

        try
        {
            await _printerClient.SendAsync(zpl, cancellationToken);

            LabelPrintsTotal.Add(
                1,
                new KeyValuePair<string, object?>("label_type", normalizedLabelType),
                new KeyValuePair<string, object?>("status", "PRINTED"));
            LabelPrintDurationMs.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            _logger.LogInformation(
                "Label printed: {LabelType}, Identifier={Identifier}",
                normalizedLabelType,
                ResolveIdentifier(normalizedData));

            return new LabelPrintResult("PRINTED");
        }
        catch (LabelPrinterUnavailableException ex)
        {
            PrinterOfflineTotal.Add(1);
            _logger.LogWarning(
                ex,
                "Printer offline, queueing print for retry for type {LabelType}",
                normalizedLabelType);

            var queueItem = _printQueueStore.Enqueue(
                normalizedLabelType,
                normalizedData,
                ex.Message);
            LabelPrintsTotal.Add(
                1,
                new KeyValuePair<string, object?>("label_type", normalizedLabelType),
                new KeyValuePair<string, object?>("status", "QUEUED"));
            LabelPrintDurationMs.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            var message = $"Print failed. Job queued for retry. Queue ID: {queueItem.Id}";
            return new LabelPrintResult("QUEUED", null, message);
        }
    }

    public async Task ProcessQueuedAsync(LabelPrintJobPayload payload, int attempt)
    {
        var normalizedData = NormalizeData(payload.Data);
        var zpl = _templateEngine.Render(payload.LabelType, normalizedData);

        try
        {
            await _printerClient.SendAsync(zpl, CancellationToken.None);

            LabelPrintsTotal.Add(
                1,
                new KeyValuePair<string, object?>("label_type", payload.LabelType),
                new KeyValuePair<string, object?>("status", "PRINTED"));
            _logger.LogInformation(
                "Queued label printed successfully: {LabelType}, RequestId={RequestId}, Attempt={Attempt}",
                payload.LabelType,
                payload.RequestId,
                attempt);
        }
        catch (Exception ex)
        {
            PrinterOfflineTotal.Add(1);
            if (attempt < 3)
            {
                var retryDelay = attempt switch
                {
                    1 => TimeSpan.FromMinutes(2),
                    2 => TimeSpan.FromMinutes(4),
                    _ => TimeSpan.FromMinutes(1)
                };

                _backgroundJobs.Schedule<LabelPrintOrchestrator>(
                    x => x.ProcessQueuedAsync(payload, attempt + 1),
                    retryDelay);

                _logger.LogWarning(
                    ex,
                    "Queued label print retry scheduled: {LabelType}, RequestId={RequestId}, NextAttempt={NextAttempt}, DelayMinutes={Delay}",
                    payload.LabelType,
                    payload.RequestId,
                    attempt + 1,
                    retryDelay.TotalMinutes);

                return;
            }

            var pdfUrl = await CreatePdfFallbackAsync(payload, zpl, CancellationToken.None);
            LabelPrintsTotal.Add(
                1,
                new KeyValuePair<string, object?>("label_type", payload.LabelType),
                new KeyValuePair<string, object?>("status", "PDF_FALLBACK"));

            _logger.LogError(
                ex,
                "Label print failed after retries; PDF fallback generated at {PdfUrl}. RequestId={RequestId}",
                pdfUrl,
                payload.RequestId);
        }
    }

    public async Task<LabelPreviewResult> GeneratePreviewAsync(
        string labelType,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return _templateEngine.BuildPreview(labelType, NormalizeData(data));
    }

    public async Task<LabelPdfFileResult?> GetPdfAsync(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName) ||
            fileName.Contains(Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            fileName.Contains(Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            return null;
        }

        var path = Path.Combine(_outputRootPath, "pdf", fileName);
        if (!File.Exists(path))
        {
            return null;
        }

        var content = await File.ReadAllBytesAsync(path, cancellationToken);
        return new LabelPdfFileResult(content, "application/pdf", fileName);
    }

    private async Task<string> CreatePdfFallbackAsync(
        LabelPrintJobPayload payload,
        string zpl,
        CancellationToken cancellationToken)
    {
        var directory = Path.Combine(_outputRootPath, "pdf");
        Directory.CreateDirectory(directory);

        var fileName = $"label-{payload.RequestId:N}.pdf";
        var filePath = Path.Combine(directory, fileName);
        var pdfBytes = BuildSimplePdfBytes(zpl);
        await File.WriteAllBytesAsync(filePath, pdfBytes, cancellationToken);

        return $"/api/warehouse/v1/labels/pdf/{fileName}";
    }

    private static Dictionary<string, string> NormalizeData(IReadOnlyDictionary<string, string> data)
    {
        return new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveIdentifier(IReadOnlyDictionary<string, string> data)
    {
        if (data.TryGetValue("LocationCode", out var locationCode))
        {
            return locationCode;
        }

        if (data.TryGetValue("Lpn", out var lpn))
        {
            return lpn;
        }

        if (data.TryGetValue("ItemCode", out var itemCode))
        {
            return itemCode;
        }

        return "n/a";
    }

    private static byte[] BuildSimplePdfBytes(string body)
    {
        var lines = body
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(70)
            .Select(EscapePdfText)
            .ToArray();

        var contentBuilder = new StringBuilder();
        var currentY = 780;
        foreach (var line in lines)
        {
            contentBuilder.Append(CultureInfo.InvariantCulture, $"BT /F1 10 Tf 40 {currentY} Td ({line}) Tj ET\n");
            currentY -= 12;
        }

        var contentStream = Encoding.ASCII.GetBytes(contentBuilder.ToString());
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.ASCII, leaveOpen: true);

        var offsets = new List<int>();

        void WriteObject(string value)
        {
            writer.Write(value);
            writer.Flush();
        }

        WriteObject("%PDF-1.4\n");

        offsets.Add((int)ms.Position);
        WriteObject("1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n");

        offsets.Add((int)ms.Position);
        WriteObject("2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n");

        offsets.Add((int)ms.Position);
        WriteObject("3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >> endobj\n");

        offsets.Add((int)ms.Position);
        WriteObject("4 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Courier >> endobj\n");

        offsets.Add((int)ms.Position);
        WriteObject($"5 0 obj << /Length {contentStream.Length} >> stream\n");
        ms.Write(contentStream, 0, contentStream.Length);
        WriteObject("\nendstream endobj\n");

        var startXref = (int)ms.Position;
        WriteObject("xref\n");
        WriteObject("0 6\n");
        WriteObject("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            WriteObject($"{offset:D10} 00000 n \n");
        }

        WriteObject("trailer << /Size 6 /Root 1 0 R >>\n");
        WriteObject($"startxref\n{startXref}\n%%EOF");
        writer.Flush();

        return ms.ToArray();
    }

    private static string EscapePdfText(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }
}
