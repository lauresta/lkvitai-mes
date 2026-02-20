namespace LKvitai.MES.Contracts.Events;

public sealed class ShipmentLineSnapshot
{
    public int ItemId { get; set; }
    public string ItemSku { get; set; } = string.Empty;
    public decimal Qty { get; set; }
}

public sealed class OutboundOrderCreatedEvent : DomainEvent
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public DateTime? RequestedShipDate { get; set; }
    public List<ShipmentLineSnapshot> Lines { get; set; } = new();
}

public sealed class ShipmentPackedEvent : DomainEvent
{
    public Guid ShipmentId { get; set; }
    public string ShipmentNumber { get; set; } = string.Empty;
    public Guid OutboundOrderId { get; set; }
    public string OutboundOrderNumber { get; set; } = string.Empty;
    public Guid HandlingUnitId { get; set; }
    public string HandlingUnitCode { get; set; } = string.Empty;
    public string PackagingType { get; set; } = string.Empty;
    public List<ShipmentLineSnapshot> Lines { get; set; } = new();
    public DateTime PackedAt { get; set; }
    public string PackedBy { get; set; } = string.Empty;
    public string? LabelPreviewUrl { get; set; }
}

public sealed class ShipmentDispatchedEvent : DomainEvent
{
    public Guid ShipmentId { get; set; }
    public string ShipmentNumber { get; set; } = string.Empty;
    public Guid OutboundOrderId { get; set; }
    public string OutboundOrderNumber { get; set; } = string.Empty;
    public string Carrier { get; set; } = string.Empty;
    public string TrackingNumber { get; set; } = string.Empty;
    public string? VehicleId { get; set; }
    public DateTime DispatchedAt { get; set; }
    public string DispatchedBy { get; set; } = string.Empty;
    public bool ManualTracking { get; set; }
}
