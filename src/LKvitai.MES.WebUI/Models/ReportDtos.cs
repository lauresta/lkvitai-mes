namespace LKvitai.MES.WebUI.Models;

public record StockLevelRowDto
{
    public int? ItemId { get; init; }
    public string InternalSku { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public int? LocationId { get; init; }
    public string LocationCode { get; init; } = string.Empty;
    public string? LotNumber { get; init; }
    public DateOnly? ExpiryDate { get; init; }
    public decimal Qty { get; init; }
    public decimal ReservedQty { get; init; }
    public decimal AvailableQty { get; init; }
    public string BaseUom { get; init; } = string.Empty;
    public DateTime LastUpdated { get; init; }
}

public record StockLevelResponseDto
{
    public IReadOnlyList<StockLevelRowDto> Items { get; init; } = Array.Empty<StockLevelRowDto>();
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public DateTime ProjectionTimestamp { get; init; }
}

public record ReceivingHistoryRowDto
{
    public int Id { get; init; }
    public string ReferenceNumber { get; init; } = string.Empty;
    public int SupplierId { get; init; }
    public string SupplierName { get; init; } = string.Empty;
    public DateOnly? ExpectedDate { get; init; }
    public string Status { get; init; } = string.Empty;
    public int TotalLines { get; init; }
    public decimal TotalExpectedQty { get; init; }
    public decimal TotalReceivedQty { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset LastUpdated { get; init; }
}

public record PickHistoryRowDto
{
    public Guid TaskId { get; init; }
    public string OrderId { get; init; } = string.Empty;
    public int ItemId { get; init; }
    public string InternalSku { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public decimal Qty { get; init; }
    public decimal? PickedQty { get; init; }
    public string? AssignedToUserId { get; init; }
    public string? CompletedBy { get; init; }
    public string? FromLocationCode { get; init; }
    public string? ToLocationCode { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}
