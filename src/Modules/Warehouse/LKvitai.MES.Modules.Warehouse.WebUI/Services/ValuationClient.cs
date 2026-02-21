using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

public sealed class ValuationClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<ValuationClient>? _logger;

    public ValuationClient(IHttpClientFactory factory, ILogger<ValuationClient>? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<IReadOnlyList<OnHandValueRowDto>> GetOnHandValueAsync(
        int? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(("categoryId", categoryId?.ToString()));
        return GetAsync<IReadOnlyList<OnHandValueRowDto>>(
            $"/api/warehouse/v1/valuation/on-hand-value{query}",
            cancellationToken);
    }

    public Task<IReadOnlyList<CostHistoryRowDto>> GetCostHistoryAsync(
        int? itemId = null,
        DateTimeOffset? dateFrom = null,
        DateTimeOffset? dateTo = null,
        string? reason = null,
        string? approvedBy = null,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("itemId", itemId?.ToString()),
            ("dateFrom", dateFrom?.ToString("O")),
            ("dateTo", dateTo?.ToString("O")),
            ("reason", reason),
            ("approvedBy", approvedBy));

        return GetAsync<IReadOnlyList<CostHistoryRowDto>>(
            $"/api/warehouse/v1/valuation/cost-history{query}",
            cancellationToken);
    }

    public Task AdjustCostAsync(
        AdjustValuationCostRequestDto request,
        CancellationToken cancellationToken = default)
        => PostAsync("/api/warehouse/v1/valuation/adjust-cost", request, cancellationToken);

    public Task ApplyLandedCostAsync(
        ApplyLandedCostRequestDto request,
        CancellationToken cancellationToken = default)
        => PostAsync("/api/warehouse/v1/valuation/apply-landed-cost", request, cancellationToken);

    public Task WriteDownAsync(
        WriteDownRequestDto request,
        CancellationToken cancellationToken = default)
        => PostAsync("/api/warehouse/v1/valuation/write-down", request, cancellationToken);

    private Task<T> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken)
        => SendAndReadAsync<T>(() =>
        {
            var client = _factory.CreateClient("WarehouseApi");
            return client.GetAsync(relativeUrl, cancellationToken);
        });

    private Task PostAsync(string relativeUrl, object payload, CancellationToken cancellationToken)
        => SendAsync(() =>
        {
            var client = _factory.CreateClient("WarehouseApi");
            return client.PostAsJsonAsync(relativeUrl, payload, cancellationToken);
        });

    private async Task SendAsync(Func<Task<HttpResponseMessage>> sender)
    {
        var response = await sender();
        if (!response.IsSuccessStatusCode)
        {
            var problem = await ProblemDetailsParser.ParseAsync(response);
            _logger?.LogError(
                "Valuation API request failed. Method={Method} Uri={Uri} StatusCode={StatusCode} ErrorCode={ErrorCode} TraceId={TraceId} Detail={Detail}",
                response.RequestMessage?.Method.Method ?? "UNKNOWN",
                response.RequestMessage?.RequestUri?.ToString() ?? "UNKNOWN",
                (int)response.StatusCode,
                problem?.ErrorCode ?? "UNKNOWN",
                problem?.TraceId ?? "UNKNOWN",
                problem?.Detail ?? "n/a");

            throw new ApiException(problem, (int)response.StatusCode);
        }
    }

    private async Task<T> SendAndReadAsync<T>(Func<Task<HttpResponseMessage>> sender)
    {
        var response = await sender();
        if (!response.IsSuccessStatusCode)
        {
            var problem = await ProblemDetailsParser.ParseAsync(response);
            _logger?.LogError(
                "Valuation API request failed. Method={Method} Uri={Uri} StatusCode={StatusCode} ErrorCode={ErrorCode} TraceId={TraceId} Detail={Detail}",
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

    private static string BuildQuery(params (string Name, string? Value)[] pairs)
    {
        var included = pairs
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => $"{Uri.EscapeDataString(x.Name)}={Uri.EscapeDataString(x.Value!)}")
            .ToArray();

        return included.Length == 0 ? string.Empty : $"?{string.Join("&", included)}";
    }
}
