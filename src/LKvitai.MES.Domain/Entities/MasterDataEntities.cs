using LKvitai.MES.Domain.Common;

namespace LKvitai.MES.Domain.Entities;

public abstract class AuditableEntity : IAuditable
{
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}

public sealed class Item : AuditableEntity
{
    public int Id { get; set; }
    public string InternalSKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int CategoryId { get; set; }
    public string BaseUoM { get; set; } = string.Empty;
    public decimal? Weight { get; set; }
    public decimal? Volume { get; set; }
    public bool RequiresLotTracking { get; set; }
    public bool RequiresQC { get; set; }
    public string Status { get; set; } = "Active";
    public string? PrimaryBarcode { get; set; }
    public string? ProductConfigId { get; set; }

    public ItemCategory? Category { get; set; }
    public UnitOfMeasure? BaseUnit { get; set; }
    public ICollection<ItemUoMConversion> UomConversions { get; set; } = new List<ItemUoMConversion>();
    public ICollection<ItemBarcode> Barcodes { get; set; } = new List<ItemBarcode>();
}

public sealed class ItemCategory
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? ParentCategoryId { get; set; }

    public ItemCategory? ParentCategory { get; set; }
    public ICollection<ItemCategory> Children { get; set; } = new List<ItemCategory>();
}

public sealed class UnitOfMeasure
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public sealed class ItemUoMConversion
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public string FromUoM { get; set; } = string.Empty;
    public string ToUoM { get; set; } = string.Empty;
    public decimal Factor { get; set; }
    public string RoundingRule { get; set; } = "Up";

    public Item? Item { get; set; }
}

public sealed class ItemBarcode
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string BarcodeType { get; set; } = "Code128";
    public bool IsPrimary { get; set; }

    public Item? Item { get; set; }
}

public sealed class Supplier : AuditableEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ContactInfo { get; set; }
}

public sealed class SupplierItemMapping
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public string SupplierSKU { get; set; } = string.Empty;
    public int ItemId { get; set; }
    public int? LeadTimeDays { get; set; }
    public decimal? MinOrderQty { get; set; }
    public decimal? PricePerUnit { get; set; }

    public Supplier? Supplier { get; set; }
    public Item? Item { get; set; }
}

public sealed class Location : AuditableEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int? ParentLocationId { get; set; }
    public bool IsVirtual { get; set; }
    public decimal? MaxWeight { get; set; }
    public decimal? MaxVolume { get; set; }
    public string Status { get; set; } = "Active";
    public string? ZoneType { get; set; }

    public Location? ParentLocation { get; set; }
    public ICollection<Location> Children { get; set; } = new List<Location>();
}

public sealed class HandlingUnitType
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class Lot
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    public DateOnly? ProductionDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
}

public sealed class InboundShipment : AuditableEntity
{
    public int Id { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public int SupplierId { get; set; }
    public DateOnly? ExpectedDate { get; set; }
    public string Status { get; set; } = "Draft";

    public Supplier? Supplier { get; set; }
    public ICollection<InboundShipmentLine> Lines { get; set; } = new List<InboundShipmentLine>();
}

public sealed class InboundShipmentLine
{
    public int Id { get; set; }
    public int ShipmentId { get; set; }
    public int ItemId { get; set; }
    public decimal ExpectedQty { get; set; }
    public decimal ReceivedQty { get; set; }
    public string BaseUoM { get; set; } = string.Empty;

    public InboundShipment? Shipment { get; set; }
    public Item? Item { get; set; }
}

public sealed class AdjustmentReasonCode
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class SerialNumber
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public string Value { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
}

public sealed class SKUSequence
{
    public string Prefix { get; set; } = string.Empty;
    public int NextValue { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public sealed class EventProcessingCheckpoint
{
    public string HandlerName { get; set; } = string.Empty;
    public string StreamId { get; set; } = string.Empty;
    public long LastEventNumber { get; set; }
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PickTask : AuditableEntity
{
    public Guid TaskId { get; set; } = Guid.NewGuid();
    public string OrderId { get; set; } = string.Empty;
    public int ItemId { get; set; }
    public decimal Qty { get; set; }
    public decimal? PickedQty { get; set; }
    public int? FromLocationId { get; set; }
    public int? ToLocationId { get; set; }
    public int? LotId { get; set; }
    public string Status { get; set; } = "Pending";
    public string? AssignedToUserId { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
