using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

public sealed class GdprClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<GdprClient>? _logger;

    public GdprClient(IHttpClientFactory factory, ILogger<GdprClient>? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<IReadOnlyList<ErasureRequestDto>> GetRequestsAsync(CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<ErasureRequestDto>>("/api/warehouse/v1/admin/gdpr/erasure-requests", cancellationToken);

    public Task<ErasureRequestDto> CreateRequestAsync(CreateErasureRequestDto request, CancellationToken cancellationToken = default)
        => PostAsync<ErasureRequestDto>("/api/warehouse/v1/admin/gdpr/erasure-request", request, cancellationToken);

    public Task<ErasureRequestDto> ApproveRequestAsync(Guid id, CancellationToken cancellationToken = default)
        => PutAsync<ErasureRequestDto>($"/api/warehouse/v1/admin/gdpr/erasure-request/{id}/approve", new { }, cancellationToken);

    public Task<ErasureRequestDto> RejectRequestAsync(Guid id, string rejectionReason, CancellationToken cancellationToken = default)
        => PutAsync<ErasureRequestDto>(
            $"/api/warehouse/v1/admin/gdpr/erasure-request/{id}/reject",
            new RejectErasureRequestDto
            {
                RejectionReason = rejectionReason
            },
            cancellationToken);

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
            "GDPR request failed. Method={Method} Uri={Uri} StatusCode={StatusCode} ErrorCode={ErrorCode} TraceId={TraceId} Detail={Detail}",
            response.RequestMessage?.Method.Method ?? "UNKNOWN",
            response.RequestMessage?.RequestUri?.ToString() ?? "UNKNOWN",
            (int)response.StatusCode,
            problem?.ErrorCode ?? "UNKNOWN",
            problem?.TraceId ?? "UNKNOWN",
            problem?.Detail ?? "n/a");

        throw new ApiException(problem, (int)response.StatusCode);
    }
}
