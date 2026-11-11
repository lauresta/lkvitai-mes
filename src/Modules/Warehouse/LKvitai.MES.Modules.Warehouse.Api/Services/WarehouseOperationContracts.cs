using LKvitai.MES.Modules.Warehouse.Api.Security;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public interface IAdvancedWarehouseStore
{
    WaveRecord CreateWave(IReadOnlyCollection<Guid> orderIds, string? assignedOperator);
    IReadOnlyList<WaveRecord> GetWaves(string? status);
    WaveRecord? GetWave(Guid id);
    WaveRecord? AssignWave(Guid id, string assignedOperator);
    WaveRecord? StartWave(Guid id);

    CrossDockRecord CreateCrossDock(CrossDockCreateRequest request);
    IReadOnlyList<CrossDockRecord> GetCrossDocks();
    CrossDockRecord? UpdateCrossDockStatus(Guid id, string status);

    QcChecklistTemplateRecord CreateChecklistTemplate(QcChecklistTemplateCreateRequest request);
    IReadOnlyList<QcChecklistTemplateRecord> GetChecklistTemplates();
    QcDefectRecord CreateQcDefect(QcDefectCreateRequest request);
    IReadOnlyList<QcDefectRecord> GetQcDefects();
    QcDefectRecord? AddQcAttachment(Guid defectId, QcAttachmentRecord attachment);

    RmaRecord CreateRma(RmaCreateRequest request);
    IReadOnlyList<RmaRecord> GetRmas();
    Task<RmaRecord?> ReceiveRmaAsync(Guid id, string receivedBy, CancellationToken ct = default);
    Task<RmaRecord?> InspectRmaAsync(Guid id, string disposition, decimal? creditAmount, string inspectedBy, CancellationToken ct = default);

    SerialRecord RegisterSerial(SerialRegisterRequest request);
    SerialRecord? TransitionSerial(Guid id, SerialTransitionRequest request);
    IReadOnlyList<SerialRecord> SearchSerials(string? serial, int? itemId, string? status);

    int GetQcDefectCount();
}

public enum WaveStatus
{
    Created,
    Assigned,
    Picking,
    Completed,
    Cancelled
}

public sealed class WaveRecord
{
    public Guid Id { get; set; }
    public string WaveNumber { get; set; } = string.Empty;
    public WaveStatus Status { get; set; } = WaveStatus.Created;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? AssignedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? AssignedOperator { get; set; }
    public List<Guid> OrderIds { get; set; } = new();
    public List<WavePickLineRecord> PickList { get; set; } = new();
    public int TotalLines => PickList.Count;
    public int CompletedLines { get; set; }
}

public sealed record WavePickLineRecord(int ItemId, decimal Qty, string Location, Guid OrderId, int SortOrder, Guid TaskId, string Status);

public sealed record CrossDockCreateRequest(int InboundShipmentLineId, Guid OutboundOrderId, int ItemId, decimal Qty, string CreatedBy);
public sealed record CrossDockRecord(
    Guid Id,
    string CrossDockNumber,
    int InboundShipmentLineId,
    Guid OutboundOrderId,
    int ItemId,
    decimal Qty,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string CreatedBy);

public sealed record QcChecklistTemplateCreateRequest(
    string Name,
    string? CategoryCode,
    int? SupplierId,
    IReadOnlyList<QcChecklistTemplateItemCreateRequest> Items,
    string CreatedBy);

public sealed record QcChecklistTemplateItemCreateRequest(string Step, bool Required);
public sealed record QcChecklistTemplateRecord(
    Guid Id,
    string Name,
    string? CategoryCode,
    int? SupplierId,
    IReadOnlyList<QcChecklistItemRecord> Items,
    DateTimeOffset CreatedAt,
    string CreatedBy);

public sealed record QcChecklistItemRecord(Guid Id, int Sequence, string Step, bool Required);

public sealed record QcDefectCreateRequest(
    int ItemId,
    string? LotNumber,
    int? SupplierId,
    string DefectType,
    string Severity,
    string? Notes,
    string CreatedBy);

public sealed record QcDefectRecord(
    Guid Id,
    int ItemId,
    string? LotNumber,
    int? SupplierId,
    string DefectType,
    string Severity,
    string? Notes,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    IReadOnlyList<QcAttachmentRecord> Attachments);

public sealed record QcAttachmentRecord(Guid Id, string FileName, string ContentType, string Url, DateTimeOffset UploadedAt, string UploadedBy);

public sealed record RmaCreateRequest(Guid SalesOrderId, string Reason, IReadOnlyList<RmaLineCreateRequest> Lines, string CreatedBy);
public sealed record RmaLineCreateRequest(int ItemId, decimal Qty, string? ReasonCode, Guid? SalesOrderLineId = null);

public enum RmaStatus
{
    PendingReceipt,
    Received,
    Inspected,
    Restocked,
    Scrapped
}

public sealed class RmaRecord
{
    public Guid Id { get; set; }
    public string RmaNumber { get; set; } = string.Empty;
    public Guid SalesOrderId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public RmaStatus Status { get; set; } = RmaStatus.PendingReceipt;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReceivedAt { get; set; }
    public DateTimeOffset? InspectedAt { get; set; }
    public string? Disposition { get; set; }
    public decimal? CreditAmount { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }
    public List<RmaLineRecord> Lines { get; set; } = new();
}

public sealed record RmaLineRecord(Guid Id, int ItemId, decimal Qty, string? ReasonCode, Guid? SalesOrderLineId = null);

public sealed record SerialRegisterRequest(int ItemId, string Value, string? Location, DateOnly? WarrantyExpiryDate, string UpdatedBy);
public sealed record SerialTransitionRequest(string Status, string? Location, string UpdatedBy);

public enum SerialStatus
{
    Pending,
    Received,
    Available,
    Issued,
    Returned,
    Scrapped
}

public sealed class SerialRecord
{
    public Guid Id { get; set; }
    public int ItemId { get; set; }
    public string Value { get; set; } = string.Empty;
    public SerialStatus Status { get; set; }
    public string? Location { get; set; }
    public DateOnly? WarrantyExpiryDate { get; set; }
    public List<SerialHistoryRecord> History { get; set; } = new();
}

public sealed record SerialHistoryRecord(
    DateTimeOffset ChangedAt,
    SerialStatus? PreviousStatus,
    SerialStatus NewStatus,
    string? Location,
    string UpdatedBy);
