namespace LKvitai.MES.WebUI.Models;

public record StockLevelRowDto
{
    public int? ItemId { get; init; }
    public string InternalSku { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public int? LocationId { get; init; }
    public string LocationCode { get; init; } = string.Empty;
    public string? LotNumber { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    public decimal Qty { get; init; }
    public decimal ReservedQty { get; init; }
    public decimal AvailableQty { get; init; }
    public string BaseUom { get; init; } = string.Empty;
    public DateTime LastUpdated { get; init; }
}

public record StockLevelResponseDto
{
    public IReadOnlyList<StockLevelRowDto> Items { get; init; } = Array.Empty<StockLevelRowDto>();
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public DateTime ProjectionTimestamp { get; init; }
}

public record ReceivingHistoryRowDto
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

public record PickHistoryRowDto
{
    public Guid TaskId { get; init; }
    public string OrderId { get; init; } = string.Empty;
    public int ItemId { get; init; }
    public string InternalSku { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public decimal Qty { get; init; }
    public decimal? PickedQty { get; init; }
    public string? AssignedToUserId { get; init; }
    public string? CompletedBy { get; init; }
    public string? FromLocationCode { get; init; }
    public string? ToLocationCode { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}

public record DispatchHistorySummaryDto
{
    public int TotalShipments { get; init; }
    public int TotalOrders { get; init; }
    public decimal OnTimeDeliveryPercent { get; init; }
}

public record DispatchHistoryReportRowDto
{
    public Guid ShipmentId { get; init; }
    public string ShipmentNumber { get; init; } = string.Empty;
    public string OrderNumber { get; init; } = string.Empty;
    public string? CustomerName { get; init; }
    public string Carrier { get; init; } = string.Empty;
    public string? TrackingNumber { get; init; }
    public DateTime DispatchDate { get; init; }
    public DateTime? DeliveryDate { get; init; }
    public DateTime? RequestedDeliveryDate { get; init; }
    public string Status { get; init; } = string.Empty;
}

public record DispatchHistoryReportResponseDto
{
    public DispatchHistorySummaryDto Summary { get; init; } = new();
    public IReadOnlyList<DispatchHistoryReportRowDto> Shipments { get; init; } = Array.Empty<DispatchHistoryReportRowDto>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public record StockMovementRowDto
{
    public DateTimeOffset Timestamp { get; init; }
    public string MovementType { get; init; } = string.Empty;
    public int ItemId { get; init; }
    public string ItemSku { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public int? FromLocationId { get; init; }
    public string? FromLocationCode { get; init; }
    public int? ToLocationId { get; init; }
    public string? ToLocationCode { get; init; }
    public decimal Qty { get; init; }
    public string BaseUoM { get; init; } = string.Empty;
    public string? Operator { get; init; }
    public string? Reason { get; init; }
    public string Reference { get; init; } = string.Empty;
}

public record StockMovementsResponseDto
{
    public IReadOnlyList<StockMovementRowDto> Movements { get; init; } = Array.Empty<StockMovementRowDto>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public record TraceabilityLotDto
{
    public string LotNumber { get; init; } = string.Empty;
    public string ItemSku { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
}

public record TraceabilityUpstreamDto
{
    public string Supplier { get; init; } = string.Empty;
    public DateTime ReceiptDate { get; init; }
    public string InboundShipment { get; init; } = string.Empty;
    public decimal QtyReceived { get; init; }
}

public record TraceabilityCurrentDto
{
    public string? Location { get; init; }
    public decimal AvailableQty { get; init; }
}

public record TraceabilityProductionOrderDto
{
    public string OrderNumber { get; init; } = string.Empty;
    public decimal QtyIssued { get; init; }
    public DateTime? IssuedDate { get; init; }
}

public record TraceabilitySalesOrderDto
{
    public string OrderNumber { get; init; } = string.Empty;
    public string Customer { get; init; } = string.Empty;
    public decimal QtyShipped { get; init; }
    public DateTime? ShippedDate { get; init; }
    public string? TrackingNumber { get; init; }
}

public record TraceabilityDownstreamDto
{
    public IReadOnlyList<TraceabilityProductionOrderDto> ProductionOrders { get; init; } = Array.Empty<TraceabilityProductionOrderDto>();
    public IReadOnlyList<TraceabilitySalesOrderDto> SalesOrders { get; init; } = Array.Empty<TraceabilitySalesOrderDto>();
}

public record TraceabilityEntryResponseDto
{
    public TraceabilityLotDto Lot { get; init; } = new();
    public TraceabilityUpstreamDto? Upstream { get; init; }
    public TraceabilityCurrentDto Current { get; init; } = new();
    public TraceabilityDownstreamDto Downstream { get; init; } = new();
    public bool IsApproximate { get; init; }
}

public record TraceabilityResponseDto
{
    public IReadOnlyList<TraceabilityEntryResponseDto> Entries { get; init; } = Array.Empty<TraceabilityEntryResponseDto>();
}

public record ComplianceAuditRowDto
{
    public DateTimeOffset Timestamp { get; init; }
    public string ReportType { get; init; } = string.Empty;
    public string Actor { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
    public string? Notes { get; init; }
}

public record ComplianceAuditResponseDto
{
    public IReadOnlyList<ComplianceAuditRowDto> Rows { get; init; } = Array.Empty<ComplianceAuditRowDto>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public record LotTraceNodeDto
{
    public string NodeType { get; init; } = string.Empty;
    public string NodeId { get; init; } = string.Empty;
    public string NodeName { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; }
    public IReadOnlyList<LotTraceNodeDto> Children { get; init; } = Array.Empty<LotTraceNodeDto>();
}

public record LotTraceResponseDto
{
    public Guid TraceId { get; init; }
    public string LotNumber { get; init; } = string.Empty;
    public string Direction { get; init; } = string.Empty;
    public bool IsApproximate { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
    public LotTraceNodeDto Root { get; init; } = new();
}
