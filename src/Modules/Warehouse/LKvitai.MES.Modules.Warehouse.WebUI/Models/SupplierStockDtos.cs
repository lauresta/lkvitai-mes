namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public record SupplierStockReportDto
{
    public int SupplierId { get; init; }
    public string SupplierCode { get; init; } = string.Empty;
    public string SupplierName { get; init; } = string.Empty;
    public int Months { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
    public IReadOnlyList<SupplierStockRowDto> Rows { get; init; } = Array.Empty<SupplierStockRowDto>();
}

public record SupplierStockRowDto
{
    public int ItemId { get; init; }
    public string ItemSku { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public string BaseUoM { get; init; } = string.Empty;
    public string? SupplierSku { get; init; }
    public decimal OnHand { get; init; }
    public decimal IssuedToProduction { get; init; }
    public decimal Dispatched { get; init; }
    public decimal WrittenOff { get; init; }
    public decimal IssuedTotal { get; init; }
    public decimal AveragePerMonth { get; init; }
    public decimal OpenInboundQty { get; init; }
    public DateOnly? NextExpectedDate { get; init; }
    public decimal? MinQty { get; init; }
    /// <summary>False when the minimum is the warehouse-wide reorder point, not the item's own.</summary>
    public bool MinQtyIsItemOwn { get; init; }
    public decimal? Shortage { get; init; }
    public decimal? PurchasePrice { get; init; }
    public decimal? SupplierPrice { get; init; }
    public decimal? LastInvoicePrice { get; init; }
    public string? LastInvoiceCurrency { get; init; }
    public DateOnly? LastInvoiceDate { get; init; }
    public decimal? UnitCost { get; init; }
    public int? LeadTimeDays { get; init; }
    public decimal? MinOrderQty { get; init; }
}

public record ProductionIssueDto
{
    public Guid MovementId { get; init; }
    public int ItemId { get; init; }
    public string ItemSku { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public decimal Qty { get; init; }
    public string BaseUoM { get; init; } = string.Empty;
    public int FromLocationId { get; init; }
    public string FromLocationCode { get; init; } = string.Empty;
    public string? Reference { get; init; }
    public string? IssuedBy { get; init; }
    public string? Notes { get; init; }
    public DateTime IssuedAt { get; init; }
}

public sealed record ProductionIssueRequestDto(int ItemId, decimal Qty, int FromLocationId, string? Reference, string? Notes);
