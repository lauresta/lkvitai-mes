namespace LKvitai.MES.Contracts.ReadModels;

public sealed class AdjustmentHistoryView
{
    public string Id { get; set; } = string.Empty;
    public Guid AdjustmentId { get; set; }
    public int ItemId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string? ItemName { get; set; }
    public int? LocationId { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? LocationCode { get; set; }
    public decimal QtyDelta { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
