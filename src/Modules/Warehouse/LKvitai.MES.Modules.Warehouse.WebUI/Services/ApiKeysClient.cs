using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

public sealed class ApiKeysClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<ApiKeysClient>? _logger;

    public ApiKeysClient(IHttpClientFactory factory, ILogger<ApiKeysClient>? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<IReadOnlyList<ApiKeyViewDto>> GetKeysAsync(CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<ApiKeyViewDto>>("/api/warehouse/v1/admin/api-keys", cancellationToken);

    public Task<ApiKeyCreatedDto> CreateKeyAsync(CreateApiKeyRequestDto request, CancellationToken cancellationToken = default)
        => PostAsync<ApiKeyCreatedDto>("/api/warehouse/v1/admin/api-keys", request, cancellationToken);

    public Task<ApiKeyCreatedDto> RotateKeyAsync(int id, CancellationToken cancellationToken = default)
        => PutAsync<ApiKeyCreatedDto>($"/api/warehouse/v1/admin/api-keys/{id}/rotate", new { }, cancellationToken);

    public async Task DeleteKeyAsync(int id, CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.DeleteAsync($"/api/warehouse/v1/admin/api-keys/{id}", cancellationToken);
        await EnsureSuccessAsync(response);
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

    private Task<T> PutAsync<T>(string relativeUrl, object payload, CancellationToken cancellationToken)
        => SendAndReadAsync<T>(() =>
        {
            var client = _factory.CreateClient("WarehouseApi");
            return client.PutAsJsonAsync(relativeUrl, payload, cancellationToken);
        });

    private async Task<T> SendAndReadAsync<T>(Func<Task<HttpResponseMessage>> sender)
    {
        var response = await sender();
        await EnsureSuccessAsync(response);

        var body = await response.Content.ReadAsStringAsync();
        var model = JsonSerializer.Deserialize<T>(body, JsonOptions);
        return model ?? throw new JsonException($"Unable to deserialize response to {typeof(T).Name}.");
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var problem = await ProblemDetailsParser.ParseAsync(response);
        _logger?.LogError(
            "API keys request failed. Method={Method} Uri={Uri} StatusCode={StatusCode} ErrorCode={ErrorCode} TraceId={TraceId} Detail={Detail}",
            response.RequestMessage?.Method.Method ?? "UNKNOWN",
            response.RequestMessage?.RequestUri?.ToString() ?? "UNKNOWN",
            (int)response.StatusCode,
            problem?.ErrorCode ?? "UNKNOWN",
            problem?.TraceId ?? "UNKNOWN",
            problem?.Detail ?? "n/a");

        throw new ApiException(problem, (int)response.StatusCode);
    }
}
