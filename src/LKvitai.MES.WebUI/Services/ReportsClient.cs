using System.Text.Json;
using LKvitai.MES.WebUI.Infrastructure;
using LKvitai.MES.WebUI.Models;

namespace LKvitai.MES.WebUI.Services;

public sealed class ReportsClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _factory;
    private readonly ILogger<ReportsClient>? _logger;

    public ReportsClient(
        IHttpClientFactory factory,
        ILogger<ReportsClient>? logger = null)
    {
        _factory = factory;
        _logger = logger;
    }

    public Task<StockLevelResponseDto> GetStockLevelAsync(
        int? itemId,
        int? locationId,
        int? categoryId,
        bool includeReserved,
        bool includeVirtualLocations,
        DateOnly? expiringBefore,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("itemId", itemId?.ToString()),
            ("locationId", locationId?.ToString()),
            ("categoryId", categoryId?.ToString()),
            ("includeReserved", includeReserved.ToString().ToLowerInvariant()),
            ("includeVirtualLocations", includeVirtualLocations.ToString().ToLowerInvariant()),
            ("expiringBefore", expiringBefore?.ToString("yyyy-MM-dd")),
            ("pageNumber", pageNumber.ToString()),
            ("pageSize", pageSize.ToString()));

        return GetAsync<StockLevelResponseDto>($"/api/warehouse/v1/stock/available{query}", cancellationToken);
    }

    public Task<byte[]> DownloadStockLevelCsvAsync(
        int? itemId,
        int? locationId,
        int? categoryId,
        bool includeReserved,
        bool includeVirtualLocations,
        DateOnly? expiringBefore,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("itemId", itemId?.ToString()),
            ("locationId", locationId?.ToString()),
            ("categoryId", categoryId?.ToString()),
            ("includeReserved", includeReserved.ToString().ToLowerInvariant()),
            ("includeVirtualLocations", includeVirtualLocations.ToString().ToLowerInvariant()),
            ("expiringBefore", expiringBefore?.ToString("yyyy-MM-dd")),
            ("exportCsv", "true"));

        return DownloadAsync($"/api/warehouse/v1/stock/available{query}", cancellationToken);
    }

    public Task<PagedApiResponse<ReceivingHistoryRowDto>> GetReceivingHistoryAsync(
        int? supplierId,
        string? status,
        DateOnly? expectedDateFrom,
        DateOnly? expectedDateTo,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("supplierId", supplierId?.ToString()),
            ("status", status),
            ("expectedDateFrom", expectedDateFrom?.ToString("yyyy-MM-dd")),
            ("expectedDateTo", expectedDateTo?.ToString("yyyy-MM-dd")),
            ("pageNumber", pageNumber.ToString()),
            ("pageSize", pageSize.ToString()));

        return GetAsync<PagedApiResponse<ReceivingHistoryRowDto>>(
            $"/api/warehouse/v1/receiving/shipments{query}",
            cancellationToken);
    }

    public Task<byte[]> DownloadReceivingHistoryCsvAsync(
        int? supplierId,
        string? status,
        DateOnly? expectedDateFrom,
        DateOnly? expectedDateTo,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("supplierId", supplierId?.ToString()),
            ("status", status),
            ("expectedDateFrom", expectedDateFrom?.ToString("yyyy-MM-dd")),
            ("expectedDateTo", expectedDateTo?.ToString("yyyy-MM-dd")),
            ("exportCsv", "true"));

        return DownloadAsync($"/api/warehouse/v1/receiving/shipments{query}", cancellationToken);
    }

    public Task<PagedApiResponse<PickHistoryRowDto>> GetPickHistoryAsync(
        string? orderId,
        string? userId,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("orderId", orderId),
            ("userId", userId),
            ("dateFrom", dateFrom?.ToString("O")),
            ("dateTo", dateTo?.ToString("O")),
            ("pageNumber", pageNumber.ToString()),
            ("pageSize", pageSize.ToString()));

        return GetAsync<PagedApiResponse<PickHistoryRowDto>>(
            $"/api/warehouse/v1/picking/history{query}",
            cancellationToken);
    }

    public Task<byte[]> DownloadPickHistoryCsvAsync(
        string? orderId,
        string? userId,
        DateTimeOffset? dateFrom,
        DateTimeOffset? dateTo,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(
            ("orderId", orderId),
            ("userId", userId),
            ("dateFrom", dateFrom?.ToString("O")),
            ("dateTo", dateTo?.ToString("O")),
            ("exportCsv", "true"));

        return DownloadAsync($"/api/warehouse/v1/picking/history{query}", cancellationToken);
    }

    private async Task<T> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.GetAsync(relativeUrl, cancellationToken);
        await EnsureSuccessAsync(response);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var model = JsonSerializer.Deserialize<T>(body, JsonOptions);
        return model ?? throw new JsonException($"Unable to deserialize response to {typeof(T).Name}.");
    }

    private async Task<byte[]> DownloadAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        var client = _factory.CreateClient("WarehouseApi");
        var response = await client.GetAsync(relativeUrl, cancellationToken);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var problem = await ProblemDetailsParser.ParseAsync(response);
        _logger?.LogError(
            "Warehouse API request failed. Method={Method} Uri={Uri} StatusCode={StatusCode} ErrorCode={ErrorCode} TraceId={TraceId} Detail={Detail}",
            response.RequestMessage?.Method.Method ?? "UNKNOWN",
            response.RequestMessage?.RequestUri?.ToString() ?? "UNKNOWN",
            (int)response.StatusCode,
            problem?.ErrorCode ?? "UNKNOWN",
            problem?.TraceId ?? "UNKNOWN",
            problem?.Detail ?? "n/a");

        throw new ApiException(problem, (int)response.StatusCode);
    }

    private static string BuildQuery(params (string Key, string? Value)[] items)
    {
        var filtered = items
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => $"{x.Key}={Uri.EscapeDataString(x.Value!)}")
            .ToList();

        return filtered.Count == 0 ? string.Empty : $"?{string.Join("&", filtered)}";
    }
}
