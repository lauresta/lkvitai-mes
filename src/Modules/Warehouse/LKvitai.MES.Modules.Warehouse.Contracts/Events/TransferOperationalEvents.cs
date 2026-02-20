namespace LKvitai.MES.Contracts.Events;

public sealed class TransferLineSnapshot
{
    public int ItemId { get; set; }
    public decimal Qty { get; set; }
    public int FromLocationId { get; set; }
    public int ToLocationId { get; set; }
}

public sealed class TransferCreatedEvent : DomainEvent
{
    public Guid TransferId { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public string FromWarehouse { get; set; } = string.Empty;
    public string ToWarehouse { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public List<TransferLineSnapshot> Lines { get; set; } = new();
}

public sealed class TransferApprovedEvent : DomainEvent
{
    public Guid TransferId { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime ApprovedAt { get; set; }
}

public sealed class TransferExecutedEvent : DomainEvent
{
    public Guid TransferId { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public string InTransitLocationCode { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public DateTime ExecutedAt { get; set; }
}

public sealed class TransferCompletedEvent : DomainEvent
{
    public Guid TransferId { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; }
}
