namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public sealed record PutawayTasksResponseDto(
    IReadOnlyList<PutawayTaskDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);

public sealed record PutawayTaskDto(
    int ItemId,
    string InternalSKU,
    string ItemName,
    decimal Qty,
    string? LotNumber,
    DateTime ReceivedAt,
    string FromLocationCode);

public sealed record PutawayRequestDto(
    int ItemId,
    decimal Qty,
    int FromLocationId,
    int ToLocationId,
    int? LotId,
    string? Notes);

public sealed record PutawayResponseDto(
    Guid EventId,
    int ItemId,
    decimal Qty,
    int FromLocationId,
    string FromLocationCode,
    int ToLocationId,
    string ToLocationCode,
    DateTime Timestamp,
    string? Warning);
