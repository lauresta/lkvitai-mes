using LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum;
using LKvitai.MES.Modules.Warehouse.Integration.Agnum;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum;

public sealed class AgnumApiClient : IAgnumApiClient
{
    private const string FallbackProductsSearchQuery = "api/products/search?code=___NO_SUCH_CODE___&filter_type=ne&order=id";

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
        var products = await FetchProductsAsync("api/products/search", ct);
        if (products is not null)
        {
            return products;
        }

        _logger.LogDebug("Agnum API returned BadRequest for empty search; retrying with fallback query to fetch all products.");
        products = await FetchProductsAsync(FallbackProductsSearchQuery, ct);
        return products ?? Array.Empty<AgnumProductDto>();
    }

    private async Task<IReadOnlyList<AgnumProductDto>?> FetchProductsAsync(string requestUri, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            request.Headers.Add("X-API-KEY", _apiKey);
        }

        _logger.LogDebug("Calling Agnum API GET {RequestUri} with X-API-KEY {ApiKeyHash}",
            requestUri,
            string.IsNullOrWhiteSpace(_apiKey) ? "<none>" : "<redacted>");

        try
        {
            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    return null;
                }

                _logger.LogWarning("Agnum API returned HTTP {StatusCode} when fetching products from {RequestUri}.", response.StatusCode, requestUri);
                return null;
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
            return null;
        }
    }
}
