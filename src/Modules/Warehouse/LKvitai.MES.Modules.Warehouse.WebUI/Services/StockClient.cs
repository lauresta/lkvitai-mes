using System.Text;
using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

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
        var query = new StringBuilder("/api/warehouse/v1/stock/available?");
        query.Append($"includeVirtualLocations={includeVirtual.ToString().ToLowerInvariant()}&pageNumber={page}&pageSize={pageSize}");

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

        return SearchCoreAsync(query.ToString(), cancellationToken);
    }

    public Task<IReadOnlyList<WarehouseDto>> GetWarehousesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<WarehouseDto>>(
        [
            new WarehouseDto
            {
                Id = "WH1",
                Code = "WH1",
                Name = "Main Warehouse"
            }
        ]);

    public async Task<PagedResult<LocationBalanceItemDto>> GetLocationBalanceAsync(
        int? locationId = null,
        string? status = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new StringBuilder("/api/warehouse/v1/stock/location-balance?");
        query.Append($"pageNumber={Math.Max(1, page)}&pageSize={Math.Clamp(pageSize, 1, 1000)}");

        if (locationId.HasValue)
        {
            query.Append("&locationId=").Append(locationId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Append("&status=").Append(Uri.EscapeDataString(status));
        }

        var payload = await GetAsync<LocationBalanceResponseDto>(query.ToString(), cancellationToken);
        return new PagedResult<LocationBalanceItemDto>
        {
            Items = payload.Items,
            TotalCount = payload.TotalCount,
            Page = payload.PageNumber,
            PageSize = payload.PageSize
        };
    }

    private async Task<PagedResult<AvailableStockItemDto>> SearchCoreAsync(string relativeUrl, CancellationToken cancellationToken)
    {
        var payload = await GetAsync<StockSearchResponseDto>(relativeUrl, cancellationToken);
        var items = payload.Items.Select(row => new AvailableStockItemDto
        {
            WarehouseId = string.IsNullOrWhiteSpace(row.WarehouseId) ? "WH1" : row.WarehouseId,
            Location = row.LocationCode ?? string.Empty,
            SKU = row.InternalSku ?? string.Empty,
            PhysicalQty = row.Qty,
            ReservedQty = row.ReservedQty,
            AvailableQty = row.AvailableQty,
            LastUpdated = row.LastUpdated
        }).ToList();

        return new PagedResult<AvailableStockItemDto>
        {
            Items = items,
            TotalCount = payload.TotalCount,
            Page = payload.PageNumber,
            PageSize = payload.PageSize
        };
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

    private sealed record StockSearchRowDto
    {
        public string WarehouseId { get; init; } = string.Empty;
        public string? LocationCode { get; init; }
        public string? InternalSku { get; init; }
        public decimal Qty { get; init; }
        public decimal ReservedQty { get; init; }
        public decimal AvailableQty { get; init; }
        public DateTime LastUpdated { get; init; }
    }

    private sealed record StockSearchResponseDto
    {
        public IReadOnlyList<StockSearchRowDto> Items { get; init; } = Array.Empty<StockSearchRowDto>();
        public int TotalCount { get; init; }
        public int PageNumber { get; init; }
        public int PageSize { get; init; }
    }

    private sealed record LocationBalanceResponseDto
    {
        public IReadOnlyList<LocationBalanceItemDto> Items { get; init; } = Array.Empty<LocationBalanceItemDto>();
        public int TotalCount { get; init; }
        public int PageNumber { get; init; }
        public int PageSize { get; init; }
    }
}
