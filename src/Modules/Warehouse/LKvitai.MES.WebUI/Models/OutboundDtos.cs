namespace LKvitai.MES.WebUI.Models;

public record OutboundOrderSummaryDto
{
    public Guid Id { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? CustomerName { get; init; }
    public int ItemCount { get; init; }
    public DateTimeOffset OrderDate { get; init; }
    public DateTimeOffset? RequestedShipDate { get; init; }
    public DateTimeOffset? PackedAt { get; init; }
    public DateTimeOffset? ShippedAt { get; init; }
    public Guid? ShipmentId { get; init; }
    public string? ShipmentNumber { get; init; }
    public string? TrackingNumber { get; init; }
}

public record OutboundOrderDetailDto
{
    public Guid Id { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? CustomerName { get; init; }
    public DateTimeOffset OrderDate { get; init; }
    public DateTimeOffset? RequestedShipDate { get; init; }
    public DateTimeOffset? PickedAt { get; init; }
    public DateTimeOffset? PackedAt { get; init; }
    public DateTimeOffset? ShippedAt { get; init; }
    public Guid ReservationId { get; init; }
    public Guid? SalesOrderId { get; init; }
    public OutboundShipmentInfoDto? Shipment { get; init; }
    public IReadOnlyList<OutboundOrderLineDto> Lines { get; init; } = Array.Empty<OutboundOrderLineDto>();
}

public record OutboundOrderLineDto
{
    public Guid Id { get; init; }
    public int ItemId { get; init; }
    public string ItemSku { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public string? PrimaryBarcode { get; init; }
    public decimal Qty { get; init; }
    public decimal PickedQty { get; init; }
    public decimal ShippedQty { get; init; }
}

public record OutboundShipmentInfoDto
{
    public Guid ShipmentId { get; init; }
    public string ShipmentNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Carrier { get; init; } = string.Empty;
    public string? TrackingNumber { get; init; }
    public DateTimeOffset? PackedAt { get; init; }
    public DateTimeOffset? DispatchedAt { get; init; }
}

public record PackOrderRequestDto
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public IReadOnlyList<ScannedItemRequestDto> ScannedItems { get; init; } = Array.Empty<ScannedItemRequestDto>();
    public string PackagingType { get; init; } = "BOX";
}

public record ScannedItemRequestDto
{
    public string Barcode { get; init; } = string.Empty;
    public decimal Qty { get; init; }
}

public record PackOrderResponseDto
{
    public Guid ShipmentId { get; init; }
    public string ShipmentNumber { get; init; } = string.Empty;
    public Guid HandlingUnitId { get; init; }
    public string HandlingUnitCode { get; init; } = string.Empty;
    public string LabelPreviewUrl { get; init; } = string.Empty;
}

public record ShipmentSummaryDto
{
    public Guid Id { get; init; }
    public string ShipmentNumber { get; init; } = string.Empty;
    public Guid OutboundOrderId { get; init; }
    public string OutboundOrderNumber { get; init; } = string.Empty;
    public string? CustomerName { get; init; }
    public string Carrier { get; init; } = string.Empty;
    public string? TrackingNumber { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset? PackedAt { get; init; }
    public DateTimeOffset? DispatchedAt { get; init; }
    public DateTimeOffset? DeliveredAt { get; init; }
    public string? PackedBy { get; init; }
    public string? DispatchedBy { get; init; }
}

public record DispatchShipmentRequestDto
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public string Carrier { get; init; } = "FEDEX";
    public string? VehicleId { get; init; }
    public DateTimeOffset? DispatchTime { get; init; }
    public string? ManualTrackingNumber { get; init; }
}

public record DispatchShipmentResponseDto
{
    public Guid ShipmentId { get; init; }
    public string ShipmentNumber { get; init; } = string.Empty;
    public string Carrier { get; init; } = string.Empty;
    public string TrackingNumber { get; init; } = string.Empty;
    public DateTime DispatchedAt { get; init; }
    public string DispatchedBy { get; init; } = string.Empty;
}
