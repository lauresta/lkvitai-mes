using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.WebUI.Infrastructure;
using LKvitai.MES.WebUI.Models;

namespace LKvitai.MES.WebUI.Services;

public sealed class CycleCountsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<CycleCountsClient>? _logger;

    public CycleCountsClient(IHttpClientFactory factory, ILogger<CycleCountsClient>? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<IReadOnlyList<CycleCountDto>> GetCycleCountsAsync(CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<CycleCountDto>>("/api/warehouse/v1/cycle-counts", cancellationToken);

    public Task<CycleCountDto> GetCycleCountByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => GetAsync<CycleCountDto>($"/api/warehouse/v1/cycle-counts/{id}", cancellationToken);

    public Task<CycleCountDto> ScheduleAsync(ScheduleCycleCountRequestDto request, CancellationToken cancellationToken = default)
        => PostAsync<CycleCountDto>("/api/warehouse/v1/cycle-counts/schedule", request, cancellationToken);

    public Task<RecordCountResponseDto> RecordCountAsync(Guid id, RecordCountRequestDto request, CancellationToken cancellationToken = default)
        => PostAsync<RecordCountResponseDto>($"/api/warehouse/v1/cycle-counts/{id}/record-count", request, cancellationToken);

    public Task<IReadOnlyList<CycleCountLineDetailDto>> GetLinesAsync(Guid id, CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<CycleCountLineDetailDto>>($"/api/warehouse/v1/cycle-counts/{id}/lines", cancellationToken);

    public Task<IReadOnlyList<DiscrepancyLineDto>> GetDiscrepanciesAsync(Guid id, CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<DiscrepancyLineDto>>($"/api/warehouse/v1/cycle-counts/{id}/discrepancies", cancellationToken);

    public Task<ApproveAdjustmentResponseDto> ApproveAdjustmentAsync(
        Guid id,
        ApproveAdjustmentRequestDto request,
        CancellationToken cancellationToken = default)
        => PostAsync<ApproveAdjustmentResponseDto>($"/api/warehouse/v1/cycle-counts/{id}/approve-adjustment", request, cancellationToken);

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
                "CycleCounts API request failed. Method={Method} Uri={Uri} StatusCode={StatusCode} ErrorCode={ErrorCode} TraceId={TraceId} Detail={Detail}",
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
