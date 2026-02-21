using LKvitai.MES.Modules.Warehouse.Domain.Common;
using LKvitai.MES.BuildingBlocks.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Domain.Entities;

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
    [Encrypted]
    public string Street { get; set; } = string.Empty;
    [Encrypted]
    public string City { get; set; } = string.Empty;
    [Encrypted]
    public string State { get; set; } = string.Empty;
    [Encrypted]
    public string ZipCode { get; set; } = string.Empty;
    [Encrypted]
    public string Country { get; set; } = string.Empty;
}

public sealed class Customer : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CustomerCode { get; set; } = string.Empty;
    [Encrypted]
    public string Name { get; set; } = string.Empty;
    [Encrypted]
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
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

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

    public Result AssignReservation(Guid reservationId)
    {
        if (reservationId == Guid.Empty)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "ReservationId is required.");
        }

        if (Status != SalesOrderStatus.Allocated)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {SalesOrderStatus.Allocated}");
        }

        ReservationId = reservationId;
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

    public Result LinkOutboundOrder(Guid outboundOrderId)
    {
        if (outboundOrderId == Guid.Empty)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "OutboundOrderId is required.");
        }

        OutboundOrderId = outboundOrderId;
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

public enum OutboundOrderType
{
    Sales,
    Transfer,
    ProductionReturn
}

public enum OutboundOrderStatus
{
    Draft,
    Allocated,
    Picking,
    Picked,
    Packed,
    Shipped,
    Delivered,
    Cancelled
}

public enum ShipmentStatus
{
    Packing,
    Packed,
    Dispatched,
    InTransit,
    Delivered,
    Cancelled
}

public enum Carrier
{
    FedEx,
    Ups,
    Dhl,
    Usps,
    Other
}

public enum TransferStatus
{
    Draft,
    PendingApproval,
    Approved,
    InTransit,
    Completed,
    Cancelled
}

public enum CycleCountStatus
{
    Scheduled,
    InProgress,
    Completed,
    Cancelled
}

public enum CycleCountLineStatus
{
    Pending,
    Approved,
    Rejected,
    Recount
}

public sealed class OutboundOrder : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OrderNumber { get; set; } = string.Empty;
    public OutboundOrderType Type { get; set; } = OutboundOrderType.Sales;
    public OutboundOrderStatus Status { get; private set; } = OutboundOrderStatus.Draft;
    public DateTimeOffset OrderDate { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RequestedShipDate { get; set; }
    public DateTimeOffset? PickedAt { get; private set; }
    public DateTimeOffset? PackedAt { get; private set; }
    public DateTimeOffset? ShippedAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public Guid ReservationId { get; set; }
    public Guid? ShipmentId { get; private set; }
    public Guid? SalesOrderId { get; set; }
    public bool IsDeleted { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Shipment? Shipment { get; set; }
    public SalesOrder? SalesOrder { get; set; }
    public ICollection<OutboundOrderLine> Lines { get; set; } = new List<OutboundOrderLine>();

    public Result MarkAllocated(Guid reservationId)
    {
        if (reservationId == Guid.Empty)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "ReservationId is required.");
        }

        if (Status != OutboundOrderStatus.Draft)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {OutboundOrderStatus.Allocated}");
        }

        ReservationId = reservationId;
        Status = OutboundOrderStatus.Allocated;
        return Result.Ok();
    }

    public Result StartPicking()
    {
        if (Status == OutboundOrderStatus.Cancelled)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Cannot start picking cancelled order.");
        }

        if (Status != OutboundOrderStatus.Allocated)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {OutboundOrderStatus.Picking}");
        }

        Status = OutboundOrderStatus.Picking;
        return Result.Ok();
    }

    public Result CompletePicking(DateTimeOffset pickedAt)
    {
        if (Status != OutboundOrderStatus.Picking)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {OutboundOrderStatus.Picked}");
        }

        Status = OutboundOrderStatus.Picked;
        PickedAt = pickedAt;
        return Result.Ok();
    }

    public Result Pack(Guid shipmentId, DateTimeOffset packedAt)
    {
        if (Status != OutboundOrderStatus.Picked)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {OutboundOrderStatus.Packed}");
        }

        ShipmentId = shipmentId;
        PackedAt = packedAt;
        Status = OutboundOrderStatus.Packed;
        return Result.Ok();
    }

    public Result Ship(DateTimeOffset shippedAt)
    {
        if (Status != OutboundOrderStatus.Packed)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {OutboundOrderStatus.Shipped}");
        }

        Status = OutboundOrderStatus.Shipped;
        ShippedAt = shippedAt;
        return Result.Ok();
    }

    public Result ConfirmDelivery(DateTimeOffset deliveredAt)
    {
        if (Status != OutboundOrderStatus.Shipped)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {OutboundOrderStatus.Delivered}");
        }

        Status = OutboundOrderStatus.Delivered;
        DeliveredAt = deliveredAt;
        return Result.Ok();
    }

    public Result Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Cancellation reason is required.");
        }

        if (Status is OutboundOrderStatus.Delivered or OutboundOrderStatus.Cancelled)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {OutboundOrderStatus.Cancelled}");
        }

        Status = OutboundOrderStatus.Cancelled;
        return Result.Ok();
    }
}

public sealed class Transfer : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TransferNumber { get; set; } = string.Empty;
    public string FromWarehouse { get; set; } = string.Empty;
    public string ToWarehouse { get; set; } = string.Empty;
    public TransferStatus Status { get; private set; } = TransferStatus.Draft;
    public string RequestedBy { get; set; } = string.Empty;
    public string? ApprovedBy { get; private set; }
    public string? ExecutedBy { get; private set; }
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SubmittedAt { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public DateTimeOffset? ExecutedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public Guid CreateCommandId { get; set; }
    public Guid? SubmitCommandId { get; private set; }
    public Guid? ApproveCommandId { get; private set; }
    public Guid? ExecuteCommandId { get; private set; }

    public ICollection<TransferLine> Lines { get; set; } = new List<TransferLine>();

    public bool RequiresApproval() => string.Equals(ToWarehouse, "SCRAP", StringComparison.OrdinalIgnoreCase);

    public Result EnsureRequestedState()
    {
        Status = TransferStatus.Draft;
        return Result.Ok();
    }

    public Result Submit(Guid commandId, DateTimeOffset submittedAt)
    {
        if (Status != TransferStatus.Draft)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {TransferStatus.PendingApproval}");
        }

        Status = RequiresApproval()
            ? TransferStatus.PendingApproval
            : TransferStatus.Approved;
        SubmitCommandId = commandId;
        SubmittedAt = submittedAt;
        return Result.Ok();
    }

    public Result Approve(string approvedBy, Guid commandId, DateTimeOffset approvedAt)
    {
        if (Status != TransferStatus.PendingApproval)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {TransferStatus.Approved}");
        }

        if (string.IsNullOrWhiteSpace(approvedBy))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "ApprovedBy is required.");
        }

        Status = TransferStatus.Approved;
        ApprovedBy = approvedBy;
        ApprovedAt = approvedAt;
        ApproveCommandId = commandId;
        return Result.Ok();
    }

    public Result StartExecution(string executedBy, Guid commandId, DateTimeOffset executedAt)
    {
        if (Status != TransferStatus.Approved)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {TransferStatus.InTransit}");
        }

        if (string.IsNullOrWhiteSpace(executedBy))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "ExecutedBy is required.");
        }

        Status = TransferStatus.InTransit;
        ExecutedBy = executedBy;
        ExecutedAt = executedAt;
        ExecuteCommandId = commandId;
        return Result.Ok();
    }

    public Result Complete(DateTimeOffset completedAt)
    {
        if (Status != TransferStatus.InTransit)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {TransferStatus.Completed}");
        }

        Status = TransferStatus.Completed;
        CompletedAt = completedAt;
        return Result.Ok();
    }
}

public sealed class TransferLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TransferId { get; set; }
    public int ItemId { get; set; }
    public decimal Qty { get; set; }
    public int FromLocationId { get; set; }
    public int ToLocationId { get; set; }
    public Guid? LotId { get; set; }

    public Transfer? Transfer { get; set; }
    public Item? Item { get; set; }
    public Location? FromLocation { get; set; }
    public Location? ToLocation { get; set; }
}

public sealed class CycleCount : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CountNumber { get; set; } = string.Empty;
    public CycleCountStatus Status { get; private set; } = CycleCountStatus.Scheduled;
    public DateTimeOffset ScheduledDate { get; set; }
    public string AbcClass { get; set; } = "ALL";
    public string AssignedOperator { get; set; } = string.Empty;
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? CountedBy { get; private set; }
    public string? ApprovedBy { get; private set; }
    public Guid ScheduleCommandId { get; set; }
    public Guid? RecordCommandId { get; private set; }
    public Guid? ApplyAdjustmentCommandId { get; private set; }

    public ICollection<CycleCountLine> Lines { get; set; } = new List<CycleCountLine>();

    public Result Start(string countedBy, Guid commandId, DateTimeOffset startedAt)
    {
        if (Status is CycleCountStatus.Completed or CycleCountStatus.Cancelled)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {CycleCountStatus.InProgress}");
        }

        Status = CycleCountStatus.InProgress;
        CountedBy = countedBy;
        StartedAt = startedAt;
        RecordCommandId = commandId;
        return Result.Ok();
    }

    public Result Complete(string approvedBy, Guid commandId, DateTimeOffset completedAt)
    {
        if (Status == CycleCountStatus.Cancelled)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {CycleCountStatus.Completed}");
        }

        Status = CycleCountStatus.Completed;
        CompletedAt = completedAt;
        ApprovedBy = approvedBy;
        ApplyAdjustmentCommandId = commandId;
        return Result.Ok();
    }

    public Result MarkCompleted(DateTimeOffset completedAt)
    {
        if (Status == CycleCountStatus.Cancelled)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {CycleCountStatus.Completed}");
        }

        Status = CycleCountStatus.Completed;
        CompletedAt = completedAt;
        return Result.Ok();
    }
}

public sealed class CycleCountLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CycleCountId { get; set; }
    public int LocationId { get; set; }
    public int ItemId { get; set; }
    public decimal SystemQty { get; set; }
    public decimal PhysicalQty { get; set; }
    public decimal Delta { get; set; }
    public DateTimeOffset? CountedAt { get; set; }
    public string? CountedBy { get; set; }
    public string? AdjustmentApprovedBy { get; set; }
    public DateTimeOffset? AdjustmentApprovedAt { get; set; }
    public CycleCountLineStatus Status { get; set; } = CycleCountLineStatus.Pending;
    public string? Reason { get; set; }

    public CycleCount? CycleCount { get; set; }
    public Location? Location { get; set; }
    public Item? Item { get; set; }
}

public sealed class OutboundOrderLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OutboundOrderId { get; set; }
    public int ItemId { get; set; }
    public decimal Qty { get; set; }
    public decimal PickedQty { get; set; }
    public decimal ShippedQty { get; set; }

    public OutboundOrder? OutboundOrder { get; set; }
    public Item? Item { get; set; }
}

public sealed class Shipment : AuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ShipmentNumber { get; set; } = string.Empty;
    public Guid OutboundOrderId { get; set; }
    public Carrier Carrier { get; private set; } = Carrier.Other;
    public string? TrackingNumber { get; private set; }
    public ShipmentStatus Status { get; private set; } = ShipmentStatus.Packing;
    public DateTimeOffset? PackedAt { get; private set; }
    public DateTimeOffset? DispatchedAt { get; private set; }
    public DateTimeOffset? InTransitAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public string? DeliverySignature { get; private set; }
    public string? DeliveryPhotoUrl { get; private set; }
    public Guid? ShippingHandlingUnitId { get; set; }
    public bool IsDeleted { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public OutboundOrder? OutboundOrder { get; set; }
    public ICollection<ShipmentLine> Lines { get; set; } = new List<ShipmentLine>();

    public Result Pack(DateTimeOffset packedAt)
    {
        if (Status != ShipmentStatus.Packing)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {ShipmentStatus.Packed}");
        }

        Status = ShipmentStatus.Packed;
        PackedAt = packedAt;
        return Result.Ok();
    }

    public Result Dispatch(Carrier carrier, string trackingNumber, DateTimeOffset dispatchedAt)
    {
        if (Status != ShipmentStatus.Packed)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {ShipmentStatus.Dispatched}");
        }

        Carrier = carrier;
        TrackingNumber = trackingNumber;
        DispatchedAt = dispatchedAt;
        Status = ShipmentStatus.Dispatched;
        return Result.Ok();
    }

    public Result MarkInTransit(DateTimeOffset inTransitAt)
    {
        if (Status != ShipmentStatus.Dispatched)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {ShipmentStatus.InTransit}");
        }

        InTransitAt = inTransitAt;
        Status = ShipmentStatus.InTransit;
        return Result.Ok();
    }

    public Result ConfirmDelivery(string signature, string? photoUrl, DateTimeOffset deliveredAt)
    {
        if (Status != ShipmentStatus.InTransit)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {ShipmentStatus.Delivered}");
        }

        if (string.IsNullOrWhiteSpace(signature))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Delivery signature is required.");
        }

        DeliverySignature = signature;
        DeliveryPhotoUrl = photoUrl;
        DeliveredAt = deliveredAt;
        Status = ShipmentStatus.Delivered;
        return Result.Ok();
    }

    public Result Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Cancellation reason is required.");
        }

        if (Status is ShipmentStatus.Delivered or ShipmentStatus.Cancelled)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Invalid status transition: {Status} -> {ShipmentStatus.Cancelled}");
        }

        Status = ShipmentStatus.Cancelled;
        return Result.Ok();
    }
}

public sealed class ShipmentLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShipmentId { get; set; }
    public int ItemId { get; set; }
    public decimal Qty { get; set; }
    public Guid? HandlingUnitId { get; set; }

    public Shipment? Shipment { get; set; }
    public Item? Item { get; set; }
}

public sealed class OutboundOrderSummary
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public int ItemCount { get; set; }
    public DateTimeOffset OrderDate { get; set; }
    public DateTimeOffset? RequestedShipDate { get; set; }
    public DateTimeOffset? PackedAt { get; set; }
    public DateTimeOffset? ShippedAt { get; set; }
    public Guid? ShipmentId { get; set; }
    public string? ShipmentNumber { get; set; }
    public string? TrackingNumber { get; set; }
}

public sealed class ShipmentSummary
{
    public Guid Id { get; set; }
    public string ShipmentNumber { get; set; } = string.Empty;
    public Guid OutboundOrderId { get; set; }
    public string OutboundOrderNumber { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string Carrier { get; set; } = string.Empty;
    public string? TrackingNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? PackedAt { get; set; }
    public DateTimeOffset? DispatchedAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public string? PackedBy { get; set; }
    public string? DispatchedBy { get; set; }
}

public sealed class DispatchHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ShipmentId { get; set; }
    public string ShipmentNumber { get; set; } = string.Empty;
    public string OutboundOrderNumber { get; set; } = string.Empty;
    public string Carrier { get; set; } = string.Empty;
    public string? TrackingNumber { get; set; }
    public string? VehicleId { get; set; }
    public DateTimeOffset DispatchedAt { get; set; }
    public string DispatchedBy { get; set; } = string.Empty;
    public bool ManualTracking { get; set; }
}

public sealed class OnHandValue
{
    public Guid Id { get; set; }
    public int ItemId { get; set; }
    public string ItemSku { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public decimal Qty { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalValue { get; set; }
    public DateTimeOffset LastUpdated { get; set; }
}

public enum AgnumExportScope
{
    ByWarehouse,
    ByCategory,
    ByLogicalWh,
    TotalOnly
}

public enum AgnumExportFormat
{
    Csv,
    JsonApi
}

public enum AgnumExportStatus
{
    Success,
    Failed,
    Retrying
}

public sealed class AgnumExportConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public AgnumExportScope Scope { get; set; } = AgnumExportScope.ByCategory;
    public string Schedule { get; set; } = "0 23 * * *";
    public AgnumExportFormat Format { get; set; } = AgnumExportFormat.Csv;
    public string? ApiEndpoint { get; set; }
    public string? ApiKey { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<AgnumMapping> Mappings { get; set; } = new List<AgnumMapping>();
}

public sealed class AgnumMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AgnumExportConfigId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string SourceValue { get; set; } = string.Empty;
    public string AgnumAccountCode { get; set; } = string.Empty;

    public AgnumExportConfig? Config { get; set; }
}

public sealed class AgnumExportHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExportConfigId { get; set; }
    public string ExportNumber { get; set; } = string.Empty;
    public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.UtcNow;
    public AgnumExportStatus Status { get; set; } = AgnumExportStatus.Retrying;
    public int RowCount { get; set; }
    public string? FilePath { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public string Trigger { get; set; } = "SCHEDULED";

    public AgnumExportConfig? ExportConfig { get; set; }
}

public enum TransactionExportFormat
{
    Csv,
    Json
}

public enum TransactionExportStatus
{
    Pending,
    Completed,
    Failed
}

public sealed class TransactionExport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public TransactionExportFormat Format { get; set; } = TransactionExportFormat.Csv;
    public int RowCount { get; set; }
    public string? FilePath { get; set; }
    public TransactionExportStatus Status { get; set; } = TransactionExportStatus.Pending;
    public string? ErrorMessage { get; set; }
    public string ExportedBy { get; set; } = string.Empty;
    public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum ComplianceReportType
{
    TransactionExport,
    LotTrace,
    VarianceAnalysis
}

public enum ComplianceReportFormat
{
    Csv,
    Pdf
}

public enum ComplianceReportStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

public sealed class ScheduledReport
{
    public int Id { get; set; }
    public ComplianceReportType ReportType { get; set; } = ComplianceReportType.TransactionExport;
    public string Schedule { get; set; } = "0 8 * * 1"; // Cron (UTC)
    public string EmailRecipients { get; set; } = string.Empty; // comma-separated
    public ComplianceReportFormat Format { get; set; } = ComplianceReportFormat.Pdf;
    public bool Active { get; set; } = true;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastRunAt { get; set; }
    public ComplianceReportStatus LastStatus { get; set; } = ComplianceReportStatus.Pending;
    public string? LastError { get; set; }
}

public sealed class GeneratedReportHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int? ScheduledReportId { get; set; }
    public ComplianceReportType ReportType { get; set; } = ComplianceReportType.TransactionExport;
    public ComplianceReportFormat Format { get; set; } = ComplianceReportFormat.Pdf;
    public ComplianceReportStatus Status { get; set; } = ComplianceReportStatus.Pending;
    public string Trigger { get; set; } = "MANUAL";
    public string FilePath { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    public ScheduledReport? ScheduledReport { get; set; }
}

public sealed class ElectronicSignature
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string SignatureText { get; set; } = string.Empty;
    public string Meaning { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string IpAddress { get; set; } = string.Empty;
    public string PreviousHash { get; set; } = string.Empty;
    public string CurrentHash { get; set; } = string.Empty;
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
    public decimal? CoordinateX { get; set; }
    public decimal? CoordinateY { get; set; }
    public decimal? CoordinateZ { get; set; }
    public decimal? WidthMeters { get; set; }
    public decimal? LengthMeters { get; set; }
    public decimal? HeightMeters { get; set; }
    public string? Aisle { get; set; }
    public string? Rack { get; set; }
    public string? Level { get; set; }
    public string? Bin { get; set; }
    public decimal? CapacityWeight { get; set; }
    public decimal? CapacityVolume { get; set; }

    public Location? ParentLocation { get; set; }
    public ICollection<Location> Children { get; set; } = new List<Location>();
}

public sealed class WarehouseLayout
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string WarehouseCode { get; set; } = string.Empty;
    public decimal WidthMeters { get; set; }
    public decimal LengthMeters { get; set; }
    public decimal HeightMeters { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ZoneDefinition> Zones { get; set; } = new List<ZoneDefinition>();
}

public sealed class ZoneDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WarehouseLayoutId { get; set; }
    public string ZoneType { get; set; } = string.Empty;
    public decimal X1 { get; set; }
    public decimal Y1 { get; set; }
    public decimal X2 { get; set; }
    public decimal Y2 { get; set; }
    public string Color { get; set; } = "#CCCCCC";

    public WarehouseLayout? WarehouseLayout { get; set; }
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

public enum ReasonCategory
{
    ADJUSTMENT = 0,
    REVALUATION = 1,
    WRITEDOWN = 2,
    RETURN = 3
}

public sealed class AdjustmentReasonCode : AuditableEntity
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? ParentId { get; set; }
    public ReasonCategory Category { get; set; } = ReasonCategory.ADJUSTMENT;
    public bool Active { get; set; } = true;
    public int UsageCount { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool IsActive
    {
        get => Active;
        set => Active = value;
    }

    public AdjustmentReasonCode? Parent { get; set; }
    public ICollection<AdjustmentReasonCode> Children { get; set; } = new List<AdjustmentReasonCode>();
}

public enum PickStrategy
{
    FEFO = 0,
    FIFO = 1
}

public sealed class WarehouseSettings : AuditableEntity
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public int CapacityThresholdPercent { get; set; } = 80;
    public PickStrategy DefaultPickStrategy { get; set; } = PickStrategy.FEFO;
    public int LowStockThreshold { get; set; } = 10;
    public int ReorderPoint { get; set; } = 50;
    public bool AutoAllocateOrders { get; set; } = true;
}

public enum ApprovalRuleType
{
    COST_ADJUSTMENT = 0,
    WRITEDOWN = 1,
    TRANSFER = 2
}

public enum ApprovalThresholdType
{
    AMOUNT = 0,
    PERCENTAGE = 1
}

public sealed class ApprovalRule : AuditableEntity
{
    public int Id { get; set; }
    public ApprovalRuleType RuleType { get; set; }
    public ApprovalThresholdType ThresholdType { get; set; }
    public decimal ThresholdValue { get; set; }
    public string ApproverRole { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
    public int Priority { get; set; } = 1;
}

public sealed class Role : AuditableEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public sealed class Permission
{
    public int Id { get; set; }
    public string Resource { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Scope { get; set; } = "ALL";

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public sealed class RolePermission
{
    public int RoleId { get; set; }
    public int PermissionId { get; set; }

    public Role? Role { get; set; }
    public Permission? Permission { get; set; }
}

public sealed class UserRoleAssignment
{
    public Guid UserId { get; set; }
    public int RoleId { get; set; }
    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
    public string AssignedBy { get; set; } = string.Empty;

    public Role? Role { get; set; }
}

public sealed class UserMfa
{
    public Guid UserId { get; set; }
    public string TotpSecret { get; set; } = string.Empty;
    public bool MfaEnabled { get; set; }
    public DateTimeOffset? MfaEnrolledAt { get; set; }
    public string BackupCodes { get; set; } = string.Empty;
    public int FailedAttempts { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class ApiKey
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = [];
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool Active { get; set; } = true;
    public int RateLimitPerMinute { get; set; } = 100;
    public DateTimeOffset? LastUsedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? PreviousKeyHash { get; set; }
    public DateTimeOffset? PreviousKeyGraceUntil { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class SecurityAuditLog
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public bool LegalHold { get; set; }
    public string Details { get; set; } = "{}";
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class EncryptedAttribute : Attribute
{
}

public sealed class PiiEncryptionKeyRecord
{
    public int Id { get; set; }
    public string KeyId { get; set; } = string.Empty;
    public bool Active { get; set; }
    public DateTimeOffset ActivatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? GraceUntil { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum ErasureRequestStatus
{
    Pending = 0,
    Approved = 1,
    Completed = 2,
    Rejected = 3
}

public sealed class ErasureRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public ErasureRequestStatus Status { get; set; } = ErasureRequestStatus.Pending;
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public string RequestedBy { get; set; } = string.Empty;
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? RejectionReason { get; set; }
}

public enum BackupType
{
    Full = 0,
    Incremental = 1
}

public enum BackupExecutionStatus
{
    Completed = 0,
    Failed = 1,
    Pending = 2
}

public sealed class BackupExecution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset BackupStartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? BackupCompletedAt { get; set; }
    public BackupType Type { get; set; } = BackupType.Full;
    public long BackupSizeBytes { get; set; }
    public string BlobPath { get; set; } = string.Empty;
    public BackupExecutionStatus Status { get; set; } = BackupExecutionStatus.Pending;
    public string? ErrorMessage { get; set; }
    public string Trigger { get; set; } = "MANUAL";
}

public enum DisasterScenario
{
    DataCenterOutage = 0,
    DatabaseCorruption = 1,
    Ransomware = 2
}

public enum DrillStatus
{
    Completed = 0,
    Failed = 1,
    InProgress = 2
}

public sealed class DRDrill
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset DrillStartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DrillCompletedAt { get; set; }
    public DisasterScenario Scenario { get; set; } = DisasterScenario.DataCenterOutage;
    public TimeSpan ActualRTO { get; set; } = TimeSpan.Zero;
    public DrillStatus Status { get; set; } = DrillStatus.InProgress;
    public string Notes { get; set; } = string.Empty;
    public string IssuesIdentifiedJson { get; set; } = "[]";
}

public enum RetentionDataType
{
    Events = 0,
    Projections = 1,
    AuditLogs = 2,
    CustomerData = 3
}

public enum RetentionExecutionStatus
{
    Completed = 0,
    Failed = 1
}

public sealed class RetentionPolicy
{
    public int Id { get; set; }
    public RetentionDataType DataType { get; set; } = RetentionDataType.AuditLogs;
    public int RetentionPeriodDays { get; set; } = 2555;
    public int? ArchiveAfterDays { get; set; }
    public int? DeleteAfterDays { get; set; }
    public bool Active { get; set; } = true;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class RetentionExecution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset ExecutedAt { get; set; } = DateTimeOffset.UtcNow;
    public int RecordsArchived { get; set; }
    public int RecordsDeleted { get; set; }
    public RetentionExecutionStatus Status { get; set; } = RetentionExecutionStatus.Completed;
    public string? ErrorMessage { get; set; }
}

public sealed class AuditLogArchive
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public bool LegalHold { get; set; }
    public string Details { get; set; } = "{}";
    public DateTimeOffset ArchivedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class EventArchive
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string StreamId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public long EventVersion { get; set; }
    public DateTimeOffset EventTimestamp { get; set; }
    public string Payload { get; set; } = "{}";
    public bool LegalHold { get; set; }
    public DateTimeOffset ArchivedAt { get; set; } = DateTimeOffset.UtcNow;
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
