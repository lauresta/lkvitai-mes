using System.Text;
using System.Text.Json;
using LKvitai.MES.WebUI.Infrastructure;
using LKvitai.MES.WebUI.Models;

namespace LKvitai.MES.WebUI.Services;

public class StockClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<StockClient>? _logger;

    public StockClient(
        IHttpClientFactory factory,
        ILogger<StockClient>? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<PagedResult<AvailableStockItemDto>> SearchAvailableStockAsync(
        string? warehouse,
        string? location,
        string? sku,
        bool includeVirtual = false,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new StringBuilder("/api/available-stock?");
        query.Append($"includeVirtual={includeVirtual.ToString().ToLowerInvariant()}&page={page}&pageSize={pageSize}");

        if (!string.IsNullOrWhiteSpace(warehouse))
        {
            query.Append("&warehouse=").Append(Uri.EscapeDataString(warehouse));
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            query.Append("&location=").Append(Uri.EscapeDataString(location));
        }

        if (!string.IsNullOrWhiteSpace(sku))
        {
            query.Append("&sku=").Append(Uri.EscapeDataString(sku));
        }

        return GetAsync<PagedResult<AvailableStockItemDto>>(query.ToString(), cancellationToken);
    }

    public async Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.GetAsync("/api/warehouses", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var problem = await ProblemDetailsParser.ParseAsync(response);
            LogFailure(response, problem);
            throw new ApiException(problem, (int)response.StatusCode);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return Array.Empty<WarehouseDto>();
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.Deserialize<IReadOnlyList<WarehouseDto>>(JsonOptions) ?? Array.Empty<WarehouseDto>();
        }

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("warehouses", out var warehouses) &&
            warehouses.ValueKind == JsonValueKind.Array)
        {
            return warehouses.Deserialize<IReadOnlyList<WarehouseDto>>(JsonOptions) ?? Array.Empty<WarehouseDto>();
        }

        throw new JsonException("Unexpected warehouses response payload.");
    }

    private async Task<T> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken = default)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.GetAsync(relativeUrl, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var problem = await ProblemDetailsParser.ParseAsync(response);
            LogFailure(response, problem);
            throw new ApiException(problem, (int)response.StatusCode);
        }

        var model = JsonSerializer.Deserialize<T>(body, JsonOptions);
        return model ?? throw new JsonException($"Unable to deserialize response to {typeof(T).Name}.");
    }

    private void LogFailure(HttpResponseMessage response, ProblemDetailsModel? problem)
    {
        _logger?.LogError(
            "Warehouse API request failed. Method={Method} Uri={Uri} StatusCode={StatusCode} ErrorCode={ErrorCode} TraceId={TraceId} Detail={Detail}",
            response.RequestMessage?.Method.Method ?? "UNKNOWN",
            response.RequestMessage?.RequestUri?.ToString() ?? "UNKNOWN",
            (int)response.StatusCode,
            problem?.ErrorCode ?? "UNKNOWN",
            problem?.TraceId ?? "UNKNOWN",
            problem?.Detail ?? "n/a");
    }
}
