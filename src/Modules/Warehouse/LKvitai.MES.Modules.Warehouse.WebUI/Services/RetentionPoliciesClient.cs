using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

public sealed class RetentionPoliciesClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<RetentionPoliciesClient>? _logger;

    public RetentionPoliciesClient(IHttpClientFactory factory, ILogger<RetentionPoliciesClient>? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<IReadOnlyList<RetentionPolicyDto>> GetPoliciesAsync(CancellationToken cancellationToken = default)
        => GetAsync<IReadOnlyList<RetentionPolicyDto>>("/api/warehouse/v1/admin/retention-policies", cancellationToken);

    public Task<RetentionPolicyDto> CreatePolicyAsync(UpsertRetentionPolicyRequestDto request, CancellationToken cancellationToken = default)
        => PostAsync<RetentionPolicyDto>("/api/warehouse/v1/admin/retention-policies", request, cancellationToken);

    public Task<RetentionPolicyDto> UpdatePolicyAsync(int id, UpsertRetentionPolicyRequestDto request, CancellationToken cancellationToken = default)
        => PutAsync<RetentionPolicyDto>($"/api/warehouse/v1/admin/retention-policies/{id}", request, cancellationToken);

    public async Task DeletePolicyAsync(int id, CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.DeleteAsync($"/api/warehouse/v1/admin/retention-policies/{id}", cancellationToken);
        await EnsureSuccessAsync(response);
    }

    public Task<RetentionExecutionDto> ExecuteAsync(CancellationToken cancellationToken = default)
        => PostAsync<RetentionExecutionDto>("/api/warehouse/v1/admin/retention-policies/execute", new { }, cancellationToken);

    public async Task SetLegalHoldAsync(long auditLogId, bool legalHold, CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.PutAsJsonAsync(
            $"/api/warehouse/v1/admin/retention-policies/legal-hold/{auditLogId}",
            new LegalHoldRequestDto { LegalHold = legalHold },
            cancellationToken);

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
            "Retention policies request failed. Method={Method} Uri={Uri} StatusCode={StatusCode} ErrorCode={ErrorCode} TraceId={TraceId} Detail={Detail}",
            response.RequestMessage?.Method.Method ?? "UNKNOWN",
            response.RequestMessage?.RequestUri?.ToString() ?? "UNKNOWN",
            (int)response.StatusCode,
            problem?.ErrorCode ?? "UNKNOWN",
            problem?.TraceId ?? "UNKNOWN",
            problem?.Detail ?? "n/a");

        throw new ApiException(problem, (int)response.StatusCode);
    }
}
