using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

public sealed class SalesOrdersClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<SalesOrdersClient>? _logger;

    public SalesOrdersClient(IHttpClientFactory factory, ILogger<SalesOrdersClient>? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<PagedResult<SalesOrderDto>> GetSalesOrdersAsync(
        string? status,
        Guid? customerId,
        DateTime? dateFrom,
        DateTime? dateTo,
        int page,
        int pageSize)
    {
        var query = BuildQueryString(new Dictionary<string, string?>
        {
            ["status"] = status,
            ["customerId"] = customerId?.ToString(),
            ["dateFrom"] = dateFrom?.ToString("O", CultureInfo.InvariantCulture),
            ["dateTo"] = dateTo?.ToString("O", CultureInfo.InvariantCulture),
            ["page"] = page.ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = pageSize.ToString(CultureInfo.InvariantCulture)
        });

        return GetAsync<PagedResult<SalesOrderDto>>($"/api/warehouse/v1/sales-orders{query}");
    }

    public Task<SalesOrderDto> GetSalesOrderAsync(Guid id)
    {
        return GetAsync<SalesOrderDto>($"/api/warehouse/v1/sales-orders/{id}");
    }

    public Task<SalesOrderDto> CreateSalesOrderAsync(SalesOrderCreateRequestDto request)
    {
        return PostAsync<SalesOrderDto>("/api/warehouse/v1/sales-orders", request);
    }

    public Task<SalesOrderDto> SubmitSalesOrderAsync(Guid id)
    {
        return PostAsync<SalesOrderDto>($"/api/warehouse/v1/sales-orders/{id}/submit", new SalesOrderCommandRequestDto());
    }

    public Task<SalesOrderDto> ApproveSalesOrderAsync(Guid id)
    {
        return PostAsync<SalesOrderDto>($"/api/warehouse/v1/sales-orders/{id}/approve", new SalesOrderCommandRequestDto());
    }

    public Task<SalesOrderAllocationResponseDto> AllocateSalesOrderAsync(Guid id)
    {
        return PostAsync<SalesOrderAllocationResponseDto>($"/api/warehouse/v1/sales-orders/{id}/allocate", new SalesOrderCommandRequestDto());
    }

    public Task<SalesOrderDto> ReleaseSalesOrderAsync(Guid id)
    {
        return PostAsync<SalesOrderDto>($"/api/warehouse/v1/sales-orders/{id}/release", new SalesOrderCommandRequestDto());
    }

    public Task<SalesOrderDto> CancelSalesOrderAsync(Guid id, string reason)
    {
        return PostAsync<SalesOrderDto>(
            $"/api/warehouse/v1/sales-orders/{id}/cancel",
            new CancelSalesOrderRequestDto
            {
                Reason = reason
            });
    }

    public Task<IReadOnlyList<SalesOrderCustomerLookupDto>> GetCustomersAsync(string? search = null)
    {
        var query = BuildQueryString(new Dictionary<string, string?>
        {
            ["search"] = search
        });

        return GetAsync<IReadOnlyList<SalesOrderCustomerLookupDto>>($"/api/warehouse/v1/customers{query}");
    }

    public Task<PagedApiResponse<AdminItemDto>> GetItemsAsync(string? search = null, int pageNumber = 1, int pageSize = 200)
    {
        var query = BuildQueryString(new Dictionary<string, string?>
        {
            ["search"] = search,
            ["status"] = "Active",
            ["pageNumber"] = pageNumber.ToString(CultureInfo.InvariantCulture),
            ["pageSize"] = pageSize.ToString(CultureInfo.InvariantCulture)
        });

        return GetAsync<PagedApiResponse<AdminItemDto>>($"/api/warehouse/v1/items{query}");
    }

    private async Task<T> GetAsync<T>(string relativeUrl)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.GetAsync(relativeUrl);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var problem = await ProblemDetailsParser.ParseAsync(response);
            LogFailure(response, problem);
            throw new ApiException(problem, (int)response.StatusCode);
        }

        var model = JsonSerializer.Deserialize<T>(body, JsonOptions);
        return model ?? throw new JsonException($"Unable to deserialize response to {typeof(T).Name}.");
    }

    private async Task<T> PostAsync<T>(string relativeUrl, object payload)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.PostAsJsonAsync(relativeUrl, payload);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var problem = await ProblemDetailsParser.ParseAsync(response);
            LogFailure(response, problem);
            throw new ApiException(problem, (int)response.StatusCode);
        }

        var model = JsonSerializer.Deserialize<T>(body, JsonOptions);
        return model ?? throw new JsonException($"Unable to deserialize response to {typeof(T).Name}.");
    }

    private static string BuildQueryString(IReadOnlyDictionary<string, string?> values)
    {
        var pairs = values
            .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
            .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}")
            .ToArray();

        return pairs.Length == 0
            ? string.Empty
            : $"?{string.Join("&", pairs)}";
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
