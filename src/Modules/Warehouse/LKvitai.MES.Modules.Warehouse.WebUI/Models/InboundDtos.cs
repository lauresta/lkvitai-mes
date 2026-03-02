namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public record InboundShipmentSummaryDto
{
    public int Id { get; init; }
    public string ReferenceNumber { get; init; } = string.Empty;
    public int SupplierId { get; init; }
    public string SupplierName { get; init; } = string.Empty;
    public DateOnly? ExpectedDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public int TotalLines { get; init; }
    public decimal TotalExpectedQty { get; init; }
    public decimal TotalReceivedQty { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset LastUpdated { get; init; }
}

public record InboundShipmentLineDetailDto
{
    public int LineId { get; init; }
    public int ItemId { get; init; }
    public string ItemSku { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public string? PrimaryBarcode { get; init; }
    public string? PrimaryThumbnailUrl { get; init; }
    public bool RequiresLotTracking { get; init; }
    public bool RequiresQC { get; init; }
    public decimal ExpectedQty { get; init; }
    public decimal ReceivedQty { get; init; }
    public string BaseUoM { get; init; } = string.Empty;
}

public record InboundShipmentDetailDto
{
    public int Id { get; init; }
    public string ReferenceNumber { get; init; } = string.Empty;
    public int SupplierId { get; init; }
    public string SupplierName { get; init; } = string.Empty;
    public DateOnly? ExpectedDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public IReadOnlyList<InboundShipmentLineDetailDto> Lines { get; init; } = Array.Empty<InboundShipmentLineDetailDto>();
}

public record CreateInboundShipmentLineRequestDto
{
    public int ItemId { get; init; }
    public decimal ExpectedQty { get; init; }
}

public record CreateInboundShipmentRequestDto
{
    public string ReferenceNumber { get; init; } = string.Empty;
    public int SupplierId { get; init; }
    public string? Type { get; init; }
    public DateOnly? ExpectedDate { get; init; }
    public IReadOnlyList<CreateInboundShipmentLineRequestDto> Lines { get; init; } = Array.Empty<CreateInboundShipmentLineRequestDto>();
}

public record ShipmentCreatedResponseDto
{
    public int Id { get; init; }
    public string ReferenceNumber { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public record ReceiveShipmentLineRequestDto
{
    public int LineId { get; init; }
    public decimal ReceivedQty { get; init; }
    public string? LotNumber { get; init; }
    public DateOnly? ProductionDate { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    public string? Notes { get; init; }
}

public record ReceiveGoodsResponseDto
{
    public int ShipmentId { get; init; }
    public int LineId { get; init; }
    public int ItemId { get; init; }
    public decimal ReceivedQty { get; init; }
    public int? LotId { get; init; }
    public int DestinationLocationId { get; init; }
    public string DestinationLocationCode { get; init; } = string.Empty;
    public Guid EventId { get; init; }
    public DateTime Timestamp { get; init; }
}

public record QcPendingRowDto
{
    public int ItemId { get; init; }
    public string ItemSku { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public int? LotId { get; init; }
    public string? LotNumber { get; init; }
    public decimal Qty { get; init; }
    public string BaseUoM { get; init; } = string.Empty;
}

public record QcActionRequestDto
{
    public int ItemId { get; init; }
    public int? LotId { get; init; }
    public decimal Qty { get; init; }
    public string? ReasonCode { get; init; }
    public string? InspectorNotes { get; init; }
    public string? SignatureText { get; init; }
    public string? SignaturePassword { get; init; }
    public string? SignatureMeaning { get; init; }
}

public record QcActionResponseDto
{
    public Guid EventId { get; init; }
    public int ItemId { get; init; }
    public decimal Qty { get; init; }
    public string DestinationLocationCode { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}
