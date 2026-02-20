using LKvitai.MES.Modules.Warehouse.Integration.Carrier;
using LKvitai.MES.SharedKernel;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

/// <summary>
/// Minimal carrier API adapter for dispatch MVP.
/// Generates deterministic tracking numbers when carrier API is enabled.
/// </summary>
public sealed class FedExApiService : ICarrierApiService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FedExApiService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public FedExApiService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<FedExApiService> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task<Result<string>> GenerateTrackingNumberAsync(
        Guid shipmentId,
        string carrier,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(carrier, "FEDEX", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Result<string>.Fail(
                DomainErrorCodes.ValidationError,
                $"Carrier '{carrier}' is not supported by FedEx adapter."));
        }

        var enabled = _configuration.GetValue("CarrierApi:Enabled", true);
        if (!enabled)
        {
            _logger.LogWarning("Carrier API disabled for shipment {ShipmentId}", shipmentId);
            return Task.FromResult(Result<string>.Fail(
                DomainErrorCodes.InternalError,
                "Carrier API is currently unavailable."));
        }

        return GenerateTrackingNumberWithFallbackAsync(shipmentId, cancellationToken);
    }

    private async Task<Result<string>> GenerateTrackingNumberWithFallbackAsync(
        Guid shipmentId,
        CancellationToken cancellationToken)
    {
        var baseUrl = _configuration["CarrierApi:FedEx:BaseUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            var localTracking = BuildLocalTrackingNumber(shipmentId);
            _logger.LogInformation(
                "FedEx endpoint is not configured. Using deterministic local tracking number for shipment {ShipmentId}: {TrackingNumber}",
                shipmentId,
                localTracking);
            return Result<string>.Ok(localTracking);
        }

        var trackingPath = _configuration["CarrierApi:FedEx:TrackingPath"]?.Trim();
        if (string.IsNullOrWhiteSpace(trackingPath))
        {
            trackingPath = "/api/v1/tracking-numbers";
        }

        try
        {
            var endpoint = new Uri(new Uri(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/"), trackingPath.TrimStart('/'));
            var client = _httpClientFactory.CreateClient("FedExApi");

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new
                    {
                        shipmentId,
                        carrier = "FEDEX",
                        requestedAtUtc = DateTime.UtcNow
                    }, JsonOptions),
                    Encoding.UTF8,
                    "application/json")
            };

            var apiKey = _configuration["CarrierApi:FedEx:ApiKey"]?.Trim();
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "FedEx API returned {StatusCode} for shipment {ShipmentId}. Body: {Body}",
                    (int)response.StatusCode,
                    shipmentId,
                    responseBody);

                return Result<string>.Fail(
                    DomainErrorCodes.InternalError,
                    $"Carrier API request failed with status {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = JsonSerializer.Deserialize<FedExTrackingResponse>(payload, JsonOptions);
            if (string.IsNullOrWhiteSpace(parsed?.TrackingNumber))
            {
                return Result<string>.Fail(
                    DomainErrorCodes.InternalError,
                    "Carrier API returned empty tracking number.");
            }

            return Result<string>.Ok(parsed.TrackingNumber.Trim());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FedEx API call failed for shipment {ShipmentId}", shipmentId);
            return Result<string>.Fail(
                DomainErrorCodes.InternalError,
                "Carrier API request failed.");
        }
    }

    private static string BuildLocalTrackingNumber(Guid shipmentId)
        => $"FDX-{DateTime.UtcNow:yyyyMMddHHmmss}-{shipmentId.ToString("N")[..8]}";

    private sealed record FedExTrackingResponse(string? TrackingNumber);
}
