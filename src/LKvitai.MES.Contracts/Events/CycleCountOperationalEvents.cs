using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Contracts.Events;

public sealed class CycleCountLineSnapshot
{
    public int LocationId { get; set; }
    public int ItemId { get; set; }
    public decimal SystemQty { get; set; }
    public decimal PhysicalQty { get; set; }
    public decimal Delta { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class CycleCountScheduledEvent : DomainEvent
{
    public Guid CycleCountId { get; set; }
    public string CountNumber { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public int LineCount { get; set; }
}

public sealed class CountRecordedEvent : DomainEvent
{
    public Guid CycleCountId { get; set; }
    public string CountNumber { get; set; } = string.Empty;
    public int LocationId { get; set; }
    public int ItemId { get; set; }
    public decimal SystemQty { get; set; }
    public decimal PhysicalQty { get; set; }
    public decimal Delta { get; set; }
    public string CountedBy { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }
}

public sealed class CycleCountCompletedEvent : DomainEvent
{
    public Guid CycleCountId { get; set; }
    public string CountNumber { get; set; } = string.Empty;
    public int ApprovedLines { get; set; }
    public int DiscrepancyLines { get; set; }
    public decimal AccuracyPercentage { get; set; }
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; }
}
