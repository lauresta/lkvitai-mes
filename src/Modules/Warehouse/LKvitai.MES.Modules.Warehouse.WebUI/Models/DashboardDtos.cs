namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public sealed record WarehouseOptionDto(string Code, string Name);

public sealed record DashboardOverviewDto
{
    public decimal TotalStockValue { get; init; }
    public int TotalSKUs { get; init; }
    public decimal TotalQuantity { get; init; }
    public int LowStockCount { get; init; }
    public int OutOfStockCount { get; init; }
    public int ExpiringSoonCount { get; init; }
    public int ExpiredCount { get; init; }
    public string AgnumStatus { get; init; } = "Unknown";
}

public sealed record CategoryValueDto(string CategoryName, decimal Value, decimal Percent);

public sealed record WarehouseValueDto(string WarehouseCode, string WarehouseName, decimal Value, decimal Quantity);

public sealed record LowStockResponseDto(IReadOnlyList<LowStockItemDto> Items, int TotalCount);

public sealed record LowStockItemDto(
    string SKU,
    string ItemName,
    string CategoryName,
    string BaseUoM,
    decimal OnHandQty,
    decimal AvailableQty,
    int ReorderPoint,
    string? SupplierName,
    int? LeadTimeDays,
    string Status);

public sealed record ExpiringLotDto(
    string SKU,
    string ItemName,
    string? LotNumber,
    string? LocationCode,
    decimal Qty,
    string BaseUoM,
    DateOnly ExpiryDate,
    int DaysRemaining,
    string Bucket);

public sealed record IncomingShipmentDto(
    string ReferenceNumber,
    string SupplierName,
    int TotalLines,
    DateOnly? ExpectedDate,
    string Status,
    decimal TotalReceivedQty,
    decimal TotalExpectedQty,
    decimal CompletionPercent,
    bool IsOverdue);

public sealed record AgnumHealthDto
{
    public string OverallStatus { get; init; } = "Unknown";
    public DateTimeOffset? ExportedAt { get; init; }
    public string ExportStatus { get; init; } = "Unknown";
    public int ExportRowCount { get; init; }
    public int ExportRetryCount { get; init; }
    public string? ExportError { get; init; }
    public DateTime? ImportStartedAt { get; init; }
    public DateTime? ImportFinishedAt { get; init; }
    public string ImportStatus { get; init; } = "Unknown";
    public int ImportProductCount { get; init; }
    public int ImportBalanceCount { get; init; }
    public int ImportErrorCount { get; init; }
    public string? ImportErrorSummary { get; init; }
    public int ReconciliationMatched { get; init; }
    public int ReconciliationOver { get; init; }
    public int ReconciliationUnder { get; init; }
    public int ReconciliationNotLinked { get; init; }
}

public sealed record ReservationFunnelDto
{
    public int Allocated { get; init; }
    public int Picking { get; init; }
    public int Consumed { get; init; }
    public int ActiveHardLocks { get; init; }
    public int StuckInPickingOver2h { get; init; }
}
