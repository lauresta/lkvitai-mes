namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public sealed record OnHandValueRowDto
{
    public Guid Id { get; init; }
    public int ItemId { get; init; }
    public string ItemSku { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public int? CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public decimal Qty { get; init; }
    public decimal UnitCost { get; init; }
    public decimal TotalValue { get; init; }
    public DateTimeOffset LastUpdated { get; init; }
}

public sealed record CostHistoryRowDto
{
    public Guid EventId { get; init; }
    public int ItemId { get; init; }
    public string ItemSku { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public DateTimeOffset ChangedAt { get; init; }
    public decimal? OldCost { get; init; }
    public decimal? NewCost { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? ApprovedBy { get; init; }
    public string ActionType { get; init; } = string.Empty;
}

public sealed record RevaluationRequestDto
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public int ItemId { get; init; }
    public decimal NewUnitPrice { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? ApprovedBy { get; init; }
}
