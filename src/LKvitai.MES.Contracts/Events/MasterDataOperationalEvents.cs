using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Contracts.Events;

/// <summary>
/// Common metadata contract for master-data operational events.
/// </summary>
public abstract class WarehouseOperationalEvent : DomainEvent
{
    public Guid AggregateId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? TraceId { get; set; }
}

public sealed class InboundShipmentCreatedEvent : WarehouseOperationalEvent
{
    public Guid ShipmentId { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public DateOnly? ExpectedDate { get; set; }
    public int TotalLines { get; set; }
    public decimal TotalExpectedQty { get; set; }
}

public sealed class GoodsReceivedEvent : WarehouseOperationalEvent
{
    public string WarehouseId { get; set; } = string.Empty;
    public Guid ShipmentId { get; set; }
    public int LineId { get; set; }
    public int ItemId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public decimal ReceivedQty { get; set; }
    public string BaseUoM { get; set; } = string.Empty;
    public int? DestinationLocationId { get; set; }
    public string DestinationLocation { get; set; } = string.Empty;
    public int? LotId { get; set; }
    public string? LotNumber { get; set; }
    public DateOnly? ProductionDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public int? SupplierId { get; set; }
    public string? Notes { get; set; }
}

public sealed class PickCompletedEvent : WarehouseOperationalEvent
{
    public string WarehouseId { get; set; } = string.Empty;
    public Guid PickTaskId { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public int ItemId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public decimal PickedQty { get; set; }
    public int? FromLocationId { get; set; }
    public string FromLocation { get; set; } = string.Empty;
    public int? ToLocationId { get; set; }
    public string ToLocation { get; set; } = string.Empty;
    public int? LotId { get; set; }
    public string? LotNumber { get; set; }
    public string? ScannedBarcode { get; set; }
    public string? Notes { get; set; }
}

public sealed class StockAdjustedEvent : WarehouseOperationalEvent
{
    public string WarehouseId { get; set; } = string.Empty;
    public Guid AdjustmentId { get; set; }
    public int ItemId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public int? LocationId { get; set; }
    public string Location { get; set; } = string.Empty;
    public int? LotId { get; set; }
    public string? LotNumber { get; set; }
    public decimal QtyDelta { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

/// <summary>
/// Master-data reservation event variant (name intentionally suffixed to avoid collisions
/// with reservation aggregate events in ReservationEvents.cs).
/// </summary>
public sealed class ReservationCreatedMasterDataEvent : WarehouseOperationalEvent
{
    public string WarehouseId { get; set; } = string.Empty;
    public Guid ReservationId { get; set; }
    public int ItemId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public decimal ReservedQty { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public int? LocationId { get; set; }
    public string Location { get; set; } = string.Empty;
    public int? LotId { get; set; }
    public string? LotNumber { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class ReservationReleasedMasterDataEvent : WarehouseOperationalEvent
{
    public string WarehouseId { get; set; } = string.Empty;
    public Guid ReservationId { get; set; }
    public int ItemId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public decimal ReleasedQty { get; set; }
    public string ReleaseReason { get; set; } = string.Empty;
}

public sealed class QCPassedEvent : WarehouseOperationalEvent
{
    public string WarehouseId { get; set; } = string.Empty;
    public int ItemId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public int? FromLocationId { get; set; }
    public string FromLocation { get; set; } = "QC_HOLD";
    public int? ToLocationId { get; set; }
    public string ToLocation { get; set; } = "RECEIVING";
    public int? LotId { get; set; }
    public string? LotNumber { get; set; }
    public string? InspectorNotes { get; set; }
}

public sealed class QCFailedEvent : WarehouseOperationalEvent
{
    public string WarehouseId { get; set; } = string.Empty;
    public int ItemId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public int? FromLocationId { get; set; }
    public string FromLocation { get; set; } = "QC_HOLD";
    public int? ToLocationId { get; set; }
    public string ToLocation { get; set; } = "QUARANTINE";
    public int? LotId { get; set; }
    public string? LotNumber { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string? InspectorNotes { get; set; }
}
