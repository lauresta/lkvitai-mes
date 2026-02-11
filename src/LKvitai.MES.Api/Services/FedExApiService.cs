using LKvitai.MES.Integration.Carrier;
using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Api.Services;

/// <summary>
/// Minimal carrier API adapter for dispatch MVP.
/// Generates deterministic tracking numbers when carrier API is enabled.
/// </summary>
public sealed class FedExApiService : ICarrierApiService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<FedExApiService> _logger;

    public FedExApiService(IConfiguration configuration, ILogger<FedExApiService> logger)
    {
        _configuration = configuration;
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

        var tracking = $"FDX-{DateTime.UtcNow:yyyyMMddHHmmss}-{shipmentId.ToString("N")[..8]}";
        return Task.FromResult(Result<string>.Ok(tracking));
    }
}
