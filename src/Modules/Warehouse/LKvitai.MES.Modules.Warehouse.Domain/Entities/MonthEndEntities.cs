namespace LKvitai.MES.Modules.Warehouse.Domain.Entities;

public static class PeriodStatus
{
    public const string Open = "Open";
    public const string Closed = "Closed";
}

/// <summary>Blueprint 4.9 "accounting_periods": one row per month the accountant ever closed.</summary>
public sealed class AccountingPeriod
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string Status { get; set; } = PeriodStatus.Open;
    public DateTimeOffset? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }
    public DateTimeOffset? ReopenedAt { get; set; }
    public string? ReopenedBy { get; set; }
    public string? ReopenReason { get; set; }
}

/// <summary>Frozen statement, bridge and acts of a closed month; later reads come from here, never from the projections.</summary>
public sealed class MonthEndSnapshot
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string ReportJson { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
