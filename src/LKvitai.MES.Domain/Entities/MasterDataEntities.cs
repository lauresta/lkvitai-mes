using LKvitai.MES.Domain.Common;
using LKvitai.MES.SharedKernel;

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

public enum CustomerStatus
{
    Active,
    OnHold,
    Inactive
}

public enum PaymentTerms
{
    Net30,
    Net60,
    Cod,
    Prepaid,
    CreditCard
}

public enum SalesOrderStatus
{
    Draft,
    PendingApproval,
    PendingStock,
    Allocated,
    Picking,
    Packed,
    Shipped,
    Delivered,
    Invoiced,
    Cancelled
}

public sealed class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public sealed class Customer : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CustomerCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public Address BillingAddress { get; set; } = new();
    public Address? DefaultShippingAddress { get; set; }
    public PaymentTerms PaymentTerms { get; set; } = PaymentTerms.Net30;
    public CustomerStatus Status { get; set; } = CustomerStatus.Active;
    public decimal? CreditLimit { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<SalesOrder> SalesOrders { get; set; } = new List<SalesOrder>();
}

public sealed class SalesOrder : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OrderNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public Address? ShippingAddress { get; set; }
    public SalesOrderStatus Status { get; private set; } = SalesOrderStatus.Draft;
    public DateOnly OrderDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly? RequestedDeliveryDate { get; set; }
    public DateTimeOffset? AllocatedAt { get; private set; }
    public DateTimeOffset? ShippedAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public DateTimeOffset? InvoicedAt { get; private set; }
    public Guid? ReservationId { get; private set; }
    public Guid? OutboundOrderId { get; private set; }
    public decimal TotalAmount { get; private set; }
    public bool IsDeleted { get; set; }

    public Customer? Customer { get; set; }
    public ICollection<SalesOrderLine> Lines { get; set; } = new List<SalesOrderLine>();

    public void RecalculateTotals()
    {
        foreach (var line in Lines)
        {
            line.LineAmount = line.OrderedQty * line.UnitPrice;
        }

        TotalAmount = Lines.Sum(x => x.LineAmount);
    }

    public Result Submit(bool requiresApproval)
    {
        if (Status != SalesOrderStatus.Draft)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {SalesOrderStatus.Allocated}");
        }

        Status = requiresApproval ? SalesOrderStatus.PendingApproval : SalesOrderStatus.Allocated;
        if (!requiresApproval)
        {
            AllocatedAt = DateTimeOffset.UtcNow;
        }

        return Result.Ok();
    }

    public Result Approve()
    {
        if (Status != SalesOrderStatus.PendingApproval)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {SalesOrderStatus.Allocated}");
        }

        Status = SalesOrderStatus.Allocated;
        AllocatedAt = DateTimeOffset.UtcNow;
        return Result.Ok();
    }

    public Result Allocate(Guid reservationId)
    {
        if (Status is not (SalesOrderStatus.Draft or SalesOrderStatus.PendingStock))
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {SalesOrderStatus.Allocated}");
        }

        ReservationId = reservationId;
        Status = SalesOrderStatus.Allocated;
        AllocatedAt = DateTimeOffset.UtcNow;
        return Result.Ok();
    }

    public Result Release()
    {
        if (Status != SalesOrderStatus.Allocated)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {SalesOrderStatus.Picking}");
        }

        Status = SalesOrderStatus.Picking;
        return Result.Ok();
    }

    public Result Pack(Guid outboundOrderId)
    {
        if (Status != SalesOrderStatus.Picking)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {SalesOrderStatus.Packed}");
        }

        OutboundOrderId = outboundOrderId;
        Status = SalesOrderStatus.Packed;
        return Result.Ok();
    }

    public Result Ship(DateTimeOffset shippedAt)
    {
        if (Status != SalesOrderStatus.Packed)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {SalesOrderStatus.Shipped}");
        }

        Status = SalesOrderStatus.Shipped;
        ShippedAt = shippedAt;
        return Result.Ok();
    }

    public Result ConfirmDelivery(DateTimeOffset deliveredAt)
    {
        if (Status != SalesOrderStatus.Shipped)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {SalesOrderStatus.Delivered}");
        }

        Status = SalesOrderStatus.Delivered;
        DeliveredAt = deliveredAt;
        return Result.Ok();
    }

    public Result Invoice(DateTimeOffset invoicedAt)
    {
        if (Status != SalesOrderStatus.Delivered)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {SalesOrderStatus.Invoiced}");
        }

        Status = SalesOrderStatus.Invoiced;
        InvoicedAt = invoicedAt;
        return Result.Ok();
    }

    public Result Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Cancellation reason is required.");
        }

        if (Status is SalesOrderStatus.Invoiced or SalesOrderStatus.Cancelled)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {SalesOrderStatus.Cancelled}");
        }

        Status = SalesOrderStatus.Cancelled;
        return Result.Ok();
    }
}

public sealed class SalesOrderLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SalesOrderId { get; set; }
    public int ItemId { get; set; }
    public decimal OrderedQty { get; set; }
    public decimal AllocatedQty { get; set; }
    public decimal PickedQty { get; set; }
    public decimal ShippedQty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineAmount { get; set; }

    public SalesOrder? SalesOrder { get; set; }
    public Item? Item { get; set; }
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
