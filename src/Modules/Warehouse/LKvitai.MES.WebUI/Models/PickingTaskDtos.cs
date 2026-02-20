namespace LKvitai.MES.WebUI.Models;

public sealed record CreatePickTaskRequestDto(
    string OrderId,
    int ItemId,
    decimal Qty,
    string? AssignedToUserId);

public sealed record PickTaskCreatedResponseDto(
    Guid TaskId,
    string OrderId,
    int ItemId,
    decimal Qty,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record CompletePickTaskRequestDto(
    int FromLocationId,
    decimal PickedQty,
    int? LotId,
    string? ScannedBarcode,
    string? ScannedLocationBarcode,
    string? Notes);

public sealed record CompletePickTaskResponseDto(
    Guid TaskId,
    Guid EventId,
    int ItemId,
    decimal PickedQty,
    int FromLocationId,
    int ToLocationId,
    string Status,
    DateTime Timestamp);

public sealed record PickLocationSuggestionDto(
    string LocationCode,
    decimal AvailableQty,
    DateOnly? ExpiryDate,
    string? LotNumber);

public sealed record PickLocationSuggestionResponseDto(
    Guid TaskId,
    int ItemId,
    string InternalSku,
    IReadOnlyList<PickLocationSuggestionDto> Locations);
