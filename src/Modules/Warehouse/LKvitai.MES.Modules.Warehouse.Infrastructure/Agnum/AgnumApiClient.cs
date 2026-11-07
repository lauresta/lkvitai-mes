using LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum;
using LKvitai.MES.Modules.Warehouse.Integration.Agnum;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Diagnostics;
using System.Text.Json;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum;

public sealed class AgnumApiClient : IAgnumApiClient
{
    private const string FallbackProductsSearchQuery = "api/products/search?code=___NO_SUCH_CODE___&filter_type=ne&order=id";
    private const string FallbackClientsSearchQuery = "api/clients/search?code=___NO_SUCH_CODE___&filter_type=ne&order=id";
    private const string AllPricesSearchQuery = "api/products/price/search?code=___NO_SUCH_CODE___&filter_type=ne&order=id";

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

    public Task<IReadOnlyList<AgnumClientDto>> GetClientsAsync(CancellationToken ct = default)
        => FetchClientsAsync(FallbackClientsSearchQuery, ct);

    public async Task<IReadOnlyDictionary<string, AgnumPriceDto>> GetPricesAsync(CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, AllPricesSearchQuery);
        if (!string.IsNullOrWhiteSpace(_apiKey)) request.Headers.Add("X-API-KEY", _apiKey);
        try
        {
            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                throw new AgnumApiUnavailableException($"Agnum API GET price/search returned HTTP {(int)response.StatusCode}.");

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new JsonException("Agnum price/search response must be an array.");

            var prices = new Dictionary<string, AgnumPriceDto>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in document.RootElement.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object) throw new JsonException("Invalid Agnum price row.");
                foreach (var product in row.EnumerateObject())
                {
                    if (product.Value.ValueKind != JsonValueKind.Object) throw new JsonException("Invalid Agnum price levels.");
                    decimal? kn1 = null, kn2 = null;
                    foreach (var level in product.Value.EnumerateObject())
                    {
                        if (level.Value.ValueKind == JsonValueKind.Null) continue;
                        if (!level.Value.TryGetDecimal(out var amount) || amount < 0)
                            throw new JsonException("Invalid Agnum price amount.");
                        if (level.Name.Equals("KN1", StringComparison.OrdinalIgnoreCase)) kn1 = amount;
                        if (level.Name.Equals("KN2", StringComparison.OrdinalIgnoreCase)) kn2 = amount;
                    }
                    if (!prices.TryAdd(product.Name, new AgnumPriceDto(kn1, kn2)))
                        throw new JsonException("Duplicate Agnum price code.");
                }
            }
            return prices;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException || ex is TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new AgnumApiUnavailableException("Agnum price/search failed.", ex);
        }
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
            var stopwatch = Stopwatch.StartNew();
            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    return null;
                }

                if ((int)response.StatusCode >= 500)
                {
                    throw new AgnumApiUnavailableException($"Agnum API GET {requestUri} returned HTTP {(int)response.StatusCode}.");
                }

                _logger.LogWarning("Agnum API returned HTTP {StatusCode} when fetching products from {RequestUri}.", response.StatusCode, requestUri);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            var products = JsonSerializer.Deserialize<IReadOnlyList<AgnumProductDto>>(content, JsonOptions);
            if (products is null)
            {
                _logger.LogInformation(
                    "Agnum API GET {RequestUri} returned no products in {ElapsedMs} ms.",
                    requestUri,
                    stopwatch.ElapsedMilliseconds);
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

            _logger.LogInformation(
                "Agnum API GET {RequestUri} returned {ProductCount} products in {ElapsedMs} ms.",
                requestUri,
                products.Count,
                stopwatch.ElapsedMilliseconds);

            return products;
        }
        catch (AgnumApiUnavailableException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new AgnumApiUnavailableException($"Agnum API GET {requestUri} failed: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Agnum API request failed while fetching products.");
            return null;
        }
    }

    private async Task<IReadOnlyList<AgnumClientDto>> FetchClientsAsync(string requestUri, CancellationToken ct)
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
            var stopwatch = Stopwatch.StartNew();
            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode >= 500)
                {
                    throw new AgnumApiUnavailableException($"Agnum API GET {requestUri} returned HTTP {(int)response.StatusCode}.");
                }

                _logger.LogWarning("Agnum API returned HTTP {StatusCode} when fetching clients from {RequestUri}.", response.StatusCode, requestUri);
                return Array.Empty<AgnumClientDto>();
            }

            var content = await response.Content.ReadAsStringAsync(ct);
            var clients = JsonSerializer.Deserialize<IReadOnlyList<AgnumClientDto>>(content, JsonOptions);
            if (clients is null)
            {
                _logger.LogInformation(
                    "Agnum API GET {RequestUri} returned no clients in {ElapsedMs} ms.",
                    requestUri,
                    stopwatch.ElapsedMilliseconds);
                return Array.Empty<AgnumClientDto>();
            }

            _logger.LogInformation(
                "Agnum API GET {RequestUri} returned {ClientCount} clients in {ElapsedMs} ms.",
                requestUri,
                clients.Count,
                stopwatch.ElapsedMilliseconds);

            return clients;
        }
        catch (AgnumApiUnavailableException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new AgnumApiUnavailableException($"Agnum API GET {requestUri} failed: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Agnum API request failed while fetching clients.");
            return Array.Empty<AgnumClientDto>();
        }
    }
}
