using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

public sealed class LabelsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<LabelsClient>? _logger;

    public LabelsClient(IHttpClientFactory factory, ILogger<LabelsClient>? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<IReadOnlyList<LabelTemplateDto>> GetTemplatesAsync(CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<LabelTemplateDto>>("/api/warehouse/v1/labels/templates", cancellationToken);

    public Task<IReadOnlyList<LabelQueueItemDto>> GetQueueAsync(CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<LabelQueueItemDto>>("/api/warehouse/v1/labels/queue", cancellationToken);

    public Task<LabelPrintResponseDto> PrintAsync(LabelPrintRequestDto request, CancellationToken cancellationToken = default)
        => PostAsync<LabelPrintResponseDto>("/api/warehouse/v1/labels/print", request, cancellationToken);

    public Task<LabelQueueItemDto> RetryAsync(Guid id, CancellationToken cancellationToken = default)
        => PostAsync<LabelQueueItemDto>($"/api/warehouse/v1/labels/queue/{id}/retry", new { }, cancellationToken);

    public async Task<byte[]> PreviewAsync(LabelPreviewRequestDto request, CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.PostAsJsonAsync("/api/warehouse/v1/labels/preview", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var problem = await ProblemDetailsParser.ParseAsync(response);
            throw new ApiException(problem, (int)response.StatusCode);
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public async Task<byte[]> GetPdfAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.GetAsync($"/api/warehouse/v1/labels/pdf/{Uri.EscapeDataString(fileName)}", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var problem = await ProblemDetailsParser.ParseAsync(response);
            throw new ApiException(problem, (int)response.StatusCode);
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private Task<T> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken)
        => SendAndReadAsync<T>(() =>
        {
            var client = _factory.CreateClient("WarehouseApi");
            return client.GetAsync(relativeUrl, cancellationToken);
        });

    private Task<T> PostAsync<T>(string relativeUrl, object payload, CancellationToken cancellationToken)
        => SendAndReadAsync<T>(() =>
        {
            var client = _factory.CreateClient("WarehouseApi");
            return client.PostAsJsonAsync(relativeUrl, payload, cancellationToken);
        });

    private async Task<T> SendAndReadAsync<T>(Func<Task<HttpResponseMessage>> sender)
    {
        var response = await sender();
        if (!response.IsSuccessStatusCode)
        {
            var problem = await ProblemDetailsParser.ParseAsync(response);
            _logger?.LogError(
                "Labels API request failed. Method={Method} Uri={Uri} StatusCode={StatusCode} ErrorCode={ErrorCode} TraceId={TraceId} Detail={Detail}",
                response.RequestMessage?.Method.Method ?? "UNKNOWN",
                response.RequestMessage?.RequestUri?.ToString() ?? "UNKNOWN",
                (int)response.StatusCode,
                problem?.ErrorCode ?? "UNKNOWN",
                problem?.TraceId ?? "UNKNOWN",
                problem?.Detail ?? "n/a");
            throw new ApiException(problem, (int)response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync();
        var model = JsonSerializer.Deserialize<T>(body, JsonOptions);
        return model ?? throw new JsonException($"Unable to deserialize response to {typeof(T).Name}.");
    }
}
