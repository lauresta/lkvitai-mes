namespace LKvitai.MES.Modules.Warehouse.Domain.Entities;

public sealed class WarehouseWave
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long Number { get; set; }
    public string Status { get; set; } = "Created";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AssignedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? AssignedOperator { get; set; }
    public List<WarehouseWaveOrder> Orders { get; set; } = [];
}
public sealed class WarehouseWaveOrder
{
    public Guid WaveId { get; set; }
    public Guid OutboundOrderId { get; set; }
}
public sealed class WarehouseRma
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long Number { get; set; }
    public Guid SalesOrderId { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "PendingReceipt";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReceivedAt { get; set; }
    public DateTimeOffset? InspectedAt { get; set; }
    public string? Disposition { get; set; }
    public decimal? CreditAmount { get; set; }
    public string CreatedBy { get; set; } = "";
    public string? UpdatedBy { get; set; }
    public List<WarehouseRmaLine> Lines { get; set; } = [];
}
public sealed class WarehouseRmaLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RmaId { get; set; }
    public Guid SalesOrderLineId { get; set; }
    public int ItemId { get; set; }
    public decimal Qty { get; set; }
    public string? ReasonCode { get; set; }
}
public sealed class WarehouseCrossDockLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long Number { get; set; }
    public int InboundShipmentLineId { get; set; }
    public Guid OutboundOrderId { get; set; }
    public int ItemId { get; set; }
    public decimal Qty { get; set; }
    public string Status { get; set; } = "PENDING";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public string CreatedBy { get; set; } = "";
}
public sealed class WarehouseQcTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string? CategoryCode { get; set; }
    public int? SupplierId { get; set; }
    public string ItemsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = "";
}
public sealed class WarehouseQcDefect
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int ItemId { get; set; }
    public string? LotNumber { get; set; }
    public int? SupplierId { get; set; }
    public string DefectType { get; set; } = "";
    public string Severity { get; set; } = "";
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = "";
    public string AttachmentsJson { get; set; } = "[]";
}
public sealed class SerialStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int SerialNumberId { get; set; }
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? PreviousStatus { get; set; }
    public string NewStatus { get; set; } = "";
    public string? Location { get; set; }
    public DateOnly? WarrantyExpiryDate { get; set; }
    public string UpdatedBy { get; set; } = "";
    public SerialNumber? SerialNumber { get; set; }
}
public sealed class LabelPrintJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TemplateType { get; set; } = "";
    public string DataJson { get; set; } = "{}";
    public string Status { get; set; } = "Pending";
    public int RetryCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastAttemptAt { get; set; }
    public string ErrorMessage { get; set; } = "";
}
public sealed class AgnumReconciliationReportEntry
{
    public Guid Id { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public string PayloadJson { get; set; } = "{}";
}
