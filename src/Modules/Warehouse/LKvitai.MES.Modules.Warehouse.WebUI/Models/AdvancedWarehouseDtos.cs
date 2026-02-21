namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public sealed record WaveCreateRequestDto(IReadOnlyList<Guid> OrderIds, string? AssignedOperator);
public sealed record AssignWaveRequestDto(string AssignedOperator);
public sealed record CompleteWaveLinesRequestDto(int Lines);

public sealed record WavePickLineDto(int ItemId, decimal Qty, string Location, Guid OrderId);

public sealed record WaveDto(
    Guid Id,
    string WaveNumber,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AssignedAt,
    DateTimeOffset? CompletedAt,
    string? AssignedOperator,
    IReadOnlyCollection<Guid> OrderIds,
    int TotalLines,
    int CompletedLines,
    IReadOnlyCollection<WavePickLineDto> PickList);

public sealed record CrossDockCreateRequestDto(Guid InboundShipmentId, Guid OutboundOrderId, int ItemId, decimal Qty);
public sealed record CrossDockStatusUpdateRequestDto(string Status);

public sealed record CrossDockDto(
    Guid Id,
    string CrossDockNumber,
    Guid InboundShipmentId,
    Guid OutboundOrderId,
    int ItemId,
    decimal Qty,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string CreatedBy);

public sealed record QcChecklistTemplateItemCreateDto(string Step, bool Required);
public sealed record QcChecklistTemplateCreateDto(
    string Name,
    string? CategoryCode,
    int? SupplierId,
    IReadOnlyList<QcChecklistTemplateItemCreateDto> Items);

public sealed record QcChecklistItemDto(Guid Id, int Sequence, string Step, bool Required);
public sealed record QcChecklistTemplateDto(
    Guid Id,
    string Name,
    string? CategoryCode,
    int? SupplierId,
    IReadOnlyList<QcChecklistItemDto> Items,
    DateTimeOffset CreatedAt,
    string CreatedBy);

public sealed record QcDefectCreateDto(
    int ItemId,
    string? LotNumber,
    int? SupplierId,
    string DefectType,
    string Severity,
    string? Notes);

public sealed record QcAttachmentDto(Guid Id, string FileName, string ContentType, string Url, DateTimeOffset UploadedAt, string UploadedBy);

public sealed record QcDefectDto(
    Guid Id,
    int ItemId,
    string? LotNumber,
    int? SupplierId,
    string DefectType,
    string Severity,
    string? Notes,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    IReadOnlyList<QcAttachmentDto> Attachments);

public sealed record RmaLineCreateDto(int ItemId, decimal Qty, string? ReasonCode);
public sealed record RmaCreateDto(Guid SalesOrderId, string Reason, IReadOnlyList<RmaLineCreateDto> Lines);
public sealed record InspectRmaRequestDto(string Disposition, decimal? CreditAmount);
public sealed record RmaLineDto(Guid Id, int ItemId, decimal Qty, string? ReasonCode);

public sealed record RmaDto(
    Guid Id,
    string RmaNumber,
    Guid SalesOrderId,
    string Reason,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReceivedAt,
    DateTimeOffset? InspectedAt,
    string? Disposition,
    decimal? CreditAmount,
    string CreatedBy,
    string? UpdatedBy,
    IReadOnlyList<RmaLineDto> Lines);

public sealed record SerialRegisterDto(int ItemId, string Value, string? Location, DateOnly? WarrantyExpiryDate);
public sealed record SerialTransitionDto(string Status, string? Location);

public sealed record SerialHistoryDto(
    DateTimeOffset ChangedAt,
    string? PreviousStatus,
    string NewStatus,
    string? Location,
    string UpdatedBy);

public sealed record SerialDto(
    Guid Id,
    int ItemId,
    string Value,
    string Status,
    string? Location,
    DateOnly? WarrantyExpiryDate,
    IReadOnlyList<SerialHistoryDto> History);

public sealed record FulfillmentKpiTrendDto(DateOnly Date, int Orders, int Shipped);
public sealed record FulfillmentKpiDto(int TotalOrders, int ShippedOrders, decimal OnTimePercent, double AveragePickMinutes, IReadOnlyList<FulfillmentKpiTrendDto> Trend);

public sealed record DefectTypeMetricDto(string DefectType, int Count);
public sealed record SupplierDefectMetricDto(int SupplierId, int Count);
public sealed record LateShipmentDto(Guid Id, string OrderNumber, DateTimeOffset? RequestedShipDate, DateTimeOffset? ShippedAt, string RootCause);
public sealed record QcLateShipmentAnalyticsDto(
    int DefectCount,
    IReadOnlyList<DefectTypeMetricDto> DefectsByType,
    IReadOnlyList<SupplierDefectMetricDto> DefectsBySupplier,
    int LateShipmentsCount,
    IReadOnlyList<LateShipmentDto> LateShipments);
