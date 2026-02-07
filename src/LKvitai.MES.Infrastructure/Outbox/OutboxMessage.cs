namespace LKvitai.MES.Infrastructure.Outbox;

/// <summary>
/// Transactional outbox message schema per blueprint
/// </summary>
public class OutboxMessage
{
    public Guid MessageId { get; set; }
    public string AggregateType { get; set; } = string.Empty;
    public Guid AggregateId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string EventData { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
}
