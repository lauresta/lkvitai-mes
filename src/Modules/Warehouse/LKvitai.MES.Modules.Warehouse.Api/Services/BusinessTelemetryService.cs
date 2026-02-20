using Microsoft.ApplicationInsights;

namespace LKvitai.MES.Api.Services;

public interface IBusinessTelemetryService
{
    void TrackOrderCreated(Guid orderId, Guid customerId, decimal totalAmount, DateTimeOffset orderDate, string orderType);

    void TrackShipmentDispatched(Guid shipmentId, Guid outboundOrderId, string carrier, DateTimeOffset dispatchedAt, TimeSpan? packingDuration);

    void TrackStockAdjusted(Guid adjustmentId, int itemId, decimal quantityDelta, string reasonCode);
}

public sealed class BusinessTelemetryService : IBusinessTelemetryService
{
    private readonly TelemetryClient? _telemetryClient;

    public BusinessTelemetryService(TelemetryClient? telemetryClient = null)
    {
        _telemetryClient = telemetryClient;
    }

    public void TrackOrderCreated(
        Guid orderId,
        Guid customerId,
        decimal totalAmount,
        DateTimeOffset orderDate,
        string orderType)
    {
        if (_telemetryClient is null)
        {
            return;
        }

        var properties = new Dictionary<string, string>
        {
            ["OrderId"] = orderId.ToString(),
            ["CustomerId"] = customerId.ToString(),
            ["OrderDate"] = orderDate.UtcDateTime.ToString("O"),
            ["OrderType"] = string.IsNullOrWhiteSpace(orderType) ? "Unknown" : orderType
        };

        var metrics = new Dictionary<string, double>
        {
            ["TotalAmount"] = (double)totalAmount,
            ["OrdersPerHour"] = 1d
        };

        _telemetryClient.TrackEvent("OrderCreated", properties, metrics);
    }

    public void TrackShipmentDispatched(
        Guid shipmentId,
        Guid outboundOrderId,
        string carrier,
        DateTimeOffset dispatchedAt,
        TimeSpan? packingDuration)
    {
        if (_telemetryClient is null)
        {
            return;
        }

        var properties = new Dictionary<string, string>
        {
            ["ShipmentId"] = shipmentId.ToString(),
            ["OutboundOrderId"] = outboundOrderId.ToString(),
            ["Carrier"] = carrier,
            ["DispatchedAt"] = dispatchedAt.UtcDateTime.ToString("O")
        };

        var metrics = new Dictionary<string, double>
        {
            ["ShipmentsPerHour"] = 1d,
            ["PackingDuration"] = packingDuration?.TotalMilliseconds ?? 0d
        };

        _telemetryClient.TrackEvent("ShipmentDispatched", properties, metrics);
    }

    public void TrackStockAdjusted(Guid adjustmentId, int itemId, decimal quantityDelta, string reasonCode)
    {
        if (_telemetryClient is null)
        {
            return;
        }

        var properties = new Dictionary<string, string>
        {
            ["AdjustmentId"] = adjustmentId.ToString(),
            ["ItemId"] = itemId.ToString(),
            ["ReasonCode"] = reasonCode
        };

        var metrics = new Dictionary<string, double>
        {
            ["QuantityDelta"] = (double)quantityDelta,
            ["PickingDuration"] = 0d
        };

        _telemetryClient.TrackEvent("StockAdjusted", properties, metrics);
    }
}
