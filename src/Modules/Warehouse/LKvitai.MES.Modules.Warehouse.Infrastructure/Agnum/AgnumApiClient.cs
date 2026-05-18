using LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum;
using LKvitai.MES.Modules.Warehouse.Integration.Agnum;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum;

public sealed class AgnumApiClient : IAgnumApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<AgnumApiClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    public AgnumApiClient(HttpClient httpClient, AgnumWarehouseKeyOptions options, ILogger<AgnumApiClient> logger)
    {
        _httpClient = httpClient;
        _apiKey = options.ApiKey?.Trim() ?? string.Empty;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AgnumProductDto>> GetProductsAsync(CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/products/search");
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            request.Headers.Add("X-API-KEY", _apiKey);
        }

        _logger.LogDebug("Calling Agnum API GET /api/products/search with X-API-KEY {ApiKeyHash}",
            string.IsNullOrWhiteSpace(_apiKey) ? "<none>" : "<redacted>");

        try
        {
            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Agnum API returned HTTP {StatusCode} when fetching products.", response.StatusCode);
                return Array.Empty<AgnumProductDto>();
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            var products = JsonSerializer.Deserialize<IReadOnlyList<AgnumProductDto>>(content, JsonOptions);
            if (products is null)
            {
                return Array.Empty<AgnumProductDto>();
            }

            foreach (var product in products)
            {
                if (!string.IsNullOrWhiteSpace(product.Barcode) && product.Barcodes is null)
                {
                    product.Barcodes = new List<string> { product.Barcode };
                }
                else if (!string.IsNullOrWhiteSpace(product.Barcode) && product.Barcodes is not null)
                {
                    if (!product.Barcodes.Contains(product.Barcode, StringComparer.Ordinal))
                    {
                        product.Barcodes.Add(product.Barcode);
                    }
                }
            }

            return products;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Agnum API request failed while fetching products.");
            return Array.Empty<AgnumProductDto>();
        }
    }
}
