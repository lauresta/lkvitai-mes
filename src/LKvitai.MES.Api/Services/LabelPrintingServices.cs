using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Hangfire;

namespace LKvitai.MES.Api.Services;

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
    string? PdfUrl = null);

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

public interface ILabelPrinterClient
{
    Task SendAsync(string zplPayload, CancellationToken cancellationToken = default);
}

public sealed class TcpLabelPrinterClient : ILabelPrinterClient
{
    private readonly IConfiguration _configuration;

    public TcpLabelPrinterClient(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendAsync(string zplPayload, CancellationToken cancellationToken = default)
    {
        var host = _configuration["Labels:PrinterHost"] ?? "127.0.0.1";
        var port = ParsePort(_configuration["Labels:PrinterPort"]);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

        using var client = new TcpClient();
        await client.ConnectAsync(host, port, timeoutCts.Token);

        await using var stream = client.GetStream();
        var bytes = Encoding.UTF8.GetBytes(zplPayload);
        await stream.WriteAsync(bytes, timeoutCts.Token);
        await stream.FlushAsync(timeoutCts.Token);
    }

    private static int ParsePort(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port)
            ? port
            : 9100;
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
    private static readonly Regex PlaceholderPattern =
        new("{{\\s*(?<key>[A-Za-z0-9_]+)\\s*}}", RegexOptions.Compiled);

    private readonly ILabelPrinterClient _printerClient;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly ILogger<LabelPrintOrchestrator> _logger;
    private readonly IReadOnlyDictionary<string, string> _templates;
    private readonly string _outputRootPath;

    public LabelPrintOrchestrator(
        ILabelPrinterClient printerClient,
        IBackgroundJobClient backgroundJobs,
        IConfiguration configuration,
        ILogger<LabelPrintOrchestrator> logger)
    {
        _printerClient = printerClient;
        _backgroundJobs = backgroundJobs;
        _logger = logger;
        _outputRootPath = configuration["Labels:OutputRootPath"] ??
                          Path.Combine(AppContext.BaseDirectory, "labels");
        _templates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["LOCATION"] = configuration["Labels:Templates:Location"] ?? DefaultLocationTemplate,
            ["HU"] = configuration["Labels:Templates:Hu"] ?? DefaultHuTemplate,
            ["ITEM"] = configuration["Labels:Templates:Item"] ?? DefaultItemTemplate
        };
    }

    public async Task<LabelPrintResult> PrintAsync(
        string labelType,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken = default)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var normalizedLabelType = NormalizeLabelType(labelType);
        var normalizedData = NormalizeData(data);
        var zpl = RenderTemplate(normalizedLabelType, normalizedData);

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
        catch (Exception ex)
        {
            PrinterOfflineTotal.Add(1);
            _logger.LogWarning(
                ex,
                "Printer offline, queuing print job for type {LabelType}",
                normalizedLabelType);

            var payload = new LabelPrintJobPayload(
                Guid.NewGuid(),
                normalizedLabelType,
                new Dictionary<string, string>(normalizedData, StringComparer.OrdinalIgnoreCase));

            try
            {
                _backgroundJobs.Schedule<LabelPrintOrchestrator>(
                    x => x.ProcessQueuedAsync(payload, 1),
                    TimeSpan.FromMinutes(1));

                LabelPrintsTotal.Add(
                    1,
                    new KeyValuePair<string, object?>("label_type", normalizedLabelType),
                    new KeyValuePair<string, object?>("status", "QUEUED"));
                LabelPrintDurationMs.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

                return new LabelPrintResult("QUEUED");
            }
            catch (Exception queueEx)
            {
                _logger.LogError(queueEx, "Failed to enqueue print job; generating PDF fallback.");
                var pdfUrl = await CreatePdfFallbackAsync(payload, zpl, cancellationToken);

                LabelPrintsTotal.Add(
                    1,
                    new KeyValuePair<string, object?>("label_type", normalizedLabelType),
                    new KeyValuePair<string, object?>("status", "PDF_FALLBACK"));
                LabelPrintDurationMs.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

                return new LabelPrintResult("PDF_FALLBACK", pdfUrl);
            }
        }
    }

    public async Task ProcessQueuedAsync(LabelPrintJobPayload payload, int attempt)
    {
        var normalizedData = NormalizeData(payload.Data);
        var zpl = RenderTemplate(payload.LabelType, normalizedData);

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
        var normalizedLabelType = NormalizeLabelType(labelType);
        var zpl = RenderTemplate(normalizedLabelType, NormalizeData(data));
        var pdf = BuildSimplePdfBytes(zpl);

        await Task.CompletedTask;
        return new LabelPreviewResult(
            pdf,
            "application/pdf",
            $"{normalizedLabelType.ToLowerInvariant()}-preview.pdf");
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

    private static string NormalizeLabelType(string labelType)
    {
        if (string.IsNullOrWhiteSpace(labelType))
        {
            throw new InvalidOperationException("Label type is required.");
        }

        return labelType.Trim().ToUpperInvariant() switch
        {
            "LOCATION" => "LOCATION",
            "HU" => "HU",
            "HANDLING_UNIT" => "HU",
            "ITEM" => "ITEM",
            _ => throw new InvalidOperationException($"Unsupported label type '{labelType}'.")
        };
    }

    private string RenderTemplate(string labelType, IReadOnlyDictionary<string, string> data)
    {
        if (!_templates.TryGetValue(labelType, out var template))
        {
            throw new InvalidOperationException($"Unsupported label type '{labelType}'.");
        }

        return PlaceholderPattern.Replace(template, match =>
        {
            var key = match.Groups["key"].Value;
            return data.TryGetValue(key, out var value) ? value : string.Empty;
        });
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

    private const string DefaultLocationTemplate = """
                                                    ^XA
                                                    ^FO50,50^A0N,50,50^FD{{LocationCode}}^FS
                                                    ^FO50,120^BY3^BCN,100,Y,N,N^FD{{Barcode}}^FS
                                                    ^FO50,240^A0N,30,30^FDCapacity: {{Capacity}} kg^FS
                                                    ^XZ
                                                    """;

    private const string DefaultHuTemplate = """
                                              ^XA
                                              ^FO50,40^A0N,40,40^FDHU {{Lpn}}^FS
                                              ^FO50,95^BY3^BCN,90,Y,N,N^FD{{Lpn}}^FS
                                              ^FO50,210^A0N,28,28^FDSKU: {{Sku}}^FS
                                              ^FO50,250^A0N,28,28^FDQTY: {{Quantity}}^FS
                                              ^XZ
                                              """;

    private const string DefaultItemTemplate = """
                                                ^XA
                                                ^FO50,40^A0N,40,40^FD{{ItemCode}}^FS
                                                ^FO50,95^BY3^BCN,90,Y,N,N^FD{{Barcode}}^FS
                                                ^FO50,210^A0N,28,28^FDName: {{ItemName}}^FS
                                                ^XZ
                                                """;
}
