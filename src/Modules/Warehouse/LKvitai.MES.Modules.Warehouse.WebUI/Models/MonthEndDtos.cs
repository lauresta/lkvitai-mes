namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public record MonthEndStatementRowDto
{
    public string StockAccount { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal Opening { get; init; }
    public decimal Receipts { get; init; }
    public decimal TrueUps { get; init; }
    public decimal Issues { get; init; }
    public decimal Returns { get; init; }
    public decimal Adjustments { get; init; }
    public decimal Scrap { get; init; }
    public decimal SupplierReturns { get; init; }
    public decimal Revaluation { get; init; }
    public decimal OpeningEntries { get; init; }
    public decimal Closing { get; init; }
    public decimal PriceDifference { get; init; }
    public decimal Residual { get; init; }
}

public record BridgeDocumentDto
{
    public string DocumentType { get; init; } = string.Empty;
    public int DocumentId { get; init; }
    public string Number { get; init; } = string.Empty;
    public DateOnly? Date { get; init; }
    public string Reason { get; init; } = string.Empty;
    public decimal Agnum { get; init; }
    public decimal Warehouse { get; init; }
    public decimal Difference { get; init; }
    public decimal DifferenceAtStart { get; init; }
}

public record BridgeAccountDto
{
    public string StockAccount { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal AgnumDebitTurnover { get; init; }
    public decimal WarehouseTurnover { get; init; }
    public decimal DifferenceAtStart { get; init; }
    public decimal DifferenceAtEnd { get; init; }
    public decimal Explained { get; init; }
    public decimal Unexplained { get; init; }
    public IReadOnlyList<BridgeDocumentDto> AgnumMore { get; init; } = [];
    public IReadOnlyList<BridgeDocumentDto> WarehouseMore { get; init; } = [];
}

public record ActGroupDto
{
    public string Act { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public int Lines { get; init; }
    public decimal Qty { get; init; }
    public decimal ValueEur { get; init; }
}

public record ActRowDto
{
    public string Act { get; init; } = string.Empty;
    public DateOnly Date { get; init; }
    public string? DocumentNumber { get; init; }
    public string Sku { get; init; } = string.Empty;
    public decimal Qty { get; init; }
    public decimal ValueEur { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string StockAccount { get; init; } = string.Empty;
}

public record MonthEndReportDto
{
    public int Year { get; init; }
    public int Month { get; init; }
    public IReadOnlyList<MonthEndStatementRowDto> Statement { get; init; } = [];
    public MonthEndStatementRowDto Totals { get; init; } = new();
    public IReadOnlyList<BridgeAccountDto> Bridge { get; init; } = [];
    public IReadOnlyList<ActGroupDto> ActGroups { get; init; } = [];
    public IReadOnlyList<ActRowDto> Acts { get; init; } = [];
}

public record PeriodStateDto
{
    public int Year { get; init; }
    public int Month { get; init; }
    public string Status { get; init; } = "Open";
    public DateTimeOffset? ClosedAt { get; init; }
    public string? ClosedBy { get; init; }
    public DateTimeOffset? ReopenedAt { get; init; }
    public string? ReopenedBy { get; init; }
    public string? ReopenReason { get; init; }
    public IReadOnlyList<string> Blockers { get; init; } = [];
}
