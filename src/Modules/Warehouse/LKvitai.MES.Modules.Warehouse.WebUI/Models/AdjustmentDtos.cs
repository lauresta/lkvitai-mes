namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public sealed record AdjustmentHistoryResponseDto(
    IReadOnlyList<AdjustmentHistoryItemDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);

public sealed record AdjustmentHistoryItemDto(
    Guid AdjustmentId,
    int ItemId,
    string ItemSku,
    string? ItemName,
    int? LocationId,
    string LocationCode,
    decimal QtyDelta,
    string ReasonCode,
    string? Notes,
    string UserId,
    string? UserName,
    DateTimeOffset Timestamp);

public sealed record CreateAdjustmentRequestDto(
    int ItemId,
    int LocationId,
    decimal QtyDelta,
    string ReasonCode,
    string? Notes,
    int? LotId);

public sealed record CreateAdjustmentResponseDto(
    Guid AdjustmentId,
    Guid EventId,
    int ItemId,
    int LocationId,
    decimal QtyDelta,
    string ReasonCode,
    string UserId,
    DateTime Timestamp,
    string? Warning);
