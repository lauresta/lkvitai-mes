using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.WebUI.Infrastructure;
using LKvitai.MES.WebUI.Models;

namespace LKvitai.MES.WebUI.Services;

public sealed class AdjustmentsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<AdjustmentsClient>? _logger;

    public AdjustmentsClient(IHttpClientFactory factory, ILogger<AdjustmentsClient>? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<AdjustmentHistoryResponseDto> GetHistoryAsync(
        int? itemId,
        int? locationId,
        string? reasonCode,
        string? userId,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"pageNumber={Math.Max(1, pageNumber)}",
            $"pageSize={Math.Clamp(pageSize, 1, 500)}"
        };

        if (itemId.HasValue)
        {
            query.Add($"itemId={itemId.Value}");
        }

        if (locationId.HasValue)
        {
            query.Add($"locationId={locationId.Value}");
        }

        if (!string.IsNullOrWhiteSpace(reasonCode))
        {
            query.Add($"reasonCode={Uri.EscapeDataString(reasonCode.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query.Add($"userId={Uri.EscapeDataString(userId.Trim())}");
        }

        if (dateFrom.HasValue)
        {
            query.Add($"dateFrom={Uri.EscapeDataString(dateFrom.Value.ToString("O"))}");
        }

        if (dateTo.HasValue)
        {
            query.Add($"dateTo={Uri.EscapeDataString(dateTo.Value.ToString("O"))}");
        }

        var url = $"/api/warehouse/v1/adjustments?{string.Join("&", query)}";
        return GetAsync<AdjustmentHistoryResponseDto>(url, cancellationToken);
    }

    public Task<CreateAdjustmentResponseDto> CreateAsync(
        CreateAdjustmentRequestDto request,
        CancellationToken cancellationToken = default)
        => PostAsync<CreateAdjustmentResponseDto>("/api/warehouse/v1/adjustments", request, cancellationToken);

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
                "Adjustments API request failed. Method={Method} Uri={Uri} StatusCode={StatusCode} ErrorCode={ErrorCode} TraceId={TraceId} Detail={Detail}",
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
