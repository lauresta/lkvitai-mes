using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using LKvitai.MES.WebUI.Infrastructure;
using LKvitai.MES.WebUI.Models;

namespace LKvitai.MES.WebUI.Services;

public class OutboundClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<OutboundClient>? _logger;

    public OutboundClient(IHttpClientFactory factory, ILogger<OutboundClient>? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<IReadOnlyList<OutboundOrderSummaryDto>> GetOutboundOrderSummariesAsync(
        string? status,
        string? customer,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo)
    {
        var query = BuildQueryString(new Dictionary<string, string?>
        {
            ["status"] = status,
            ["customer"] = customer,
            ["dateFrom"] = dateFrom?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            ["dateTo"] = dateTo?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)
        });

        return GetAsync<IReadOnlyList<OutboundOrderSummaryDto>>($"/api/warehouse/v1/outbound/orders/summary{query}");
    }

    public Task<OutboundOrderDetailDto> GetOutboundOrderAsync(Guid outboundOrderId)
    {
        return GetAsync<OutboundOrderDetailDto>($"/api/warehouse/v1/outbound/orders/{outboundOrderId}");
    }

    public Task<PackOrderResponseDto> PackOrderAsync(Guid outboundOrderId, PackOrderRequestDto request)
    {
        return PostAsync<PackOrderResponseDto>($"/api/warehouse/v1/outbound/orders/{outboundOrderId}/pack", request);
    }

    public Task<IReadOnlyList<ShipmentSummaryDto>> GetShipmentSummariesAsync(
        string? status,
        string? customer,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo)
    {
        var query = BuildQueryString(new Dictionary<string, string?>
        {
            ["status"] = status,
            ["customer"] = customer,
            ["dateFrom"] = dateFrom?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            ["dateTo"] = dateTo?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)
        });

        return GetAsync<IReadOnlyList<ShipmentSummaryDto>>($"/api/warehouse/v1/shipments/summary{query}");
    }

    public Task<DispatchShipmentResponseDto> DispatchShipmentAsync(Guid shipmentId, DispatchShipmentRequestDto request)
    {
        return PostAsync<DispatchShipmentResponseDto>($"/api/warehouse/v1/shipments/{shipmentId}/dispatch", request);
    }

    public Task ReleaseSalesOrderAsync(Guid salesOrderId)
    {
        return PostAsync($"/api/warehouse/v1/sales-orders/{salesOrderId}/release", new
        {
            commandId = Guid.NewGuid()
        });
    }

    public Task CancelSalesOrderAsync(Guid salesOrderId, string reason)
    {
        return PostAsync($"/api/warehouse/v1/sales-orders/{salesOrderId}/cancel", new
        {
            commandId = Guid.NewGuid(),
            reason
        });
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

    private async Task PostAsync(string relativeUrl, object payload)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.PostAsJsonAsync(relativeUrl, payload);

        if (!response.IsSuccessStatusCode)
        {
            var problem = await ProblemDetailsParser.ParseAsync(response);
            LogFailure(response, problem);
            throw new ApiException(problem, (int)response.StatusCode);
        }
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
