using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.WebUI.Infrastructure;
using LKvitai.MES.WebUI.Models;

namespace LKvitai.MES.WebUI.Services;

public sealed class PickingTasksClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<PickingTasksClient>? _logger;

    public PickingTasksClient(IHttpClientFactory factory, ILogger<PickingTasksClient>? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<PickTaskCreatedResponseDto> CreateTaskAsync(CreatePickTaskRequestDto request, CancellationToken cancellationToken = default)
        => PostAsync<PickTaskCreatedResponseDto>("/api/warehouse/v1/picking/tasks", request, cancellationToken);

    public Task<PickLocationSuggestionResponseDto> GetLocationSuggestionsAsync(Guid taskId, CancellationToken cancellationToken = default)
        => GetAsync<PickLocationSuggestionResponseDto>($"/api/warehouse/v1/picking/tasks/{taskId}/locations", cancellationToken);

    public Task<CompletePickTaskResponseDto> CompleteTaskAsync(Guid taskId, CompletePickTaskRequestDto request, CancellationToken cancellationToken = default)
        => PostAsync<CompletePickTaskResponseDto>($"/api/warehouse/v1/picking/tasks/{taskId}/complete", request, cancellationToken);

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
                "Picking tasks API request failed. Method={Method} Uri={Uri} StatusCode={StatusCode} ErrorCode={ErrorCode} TraceId={TraceId} Detail={Detail}",
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
