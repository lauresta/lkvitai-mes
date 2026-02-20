namespace LKvitai.MES.Contracts.Events;

/// <summary>
/// Contract-level event base type, kept local so Contracts remains independent
/// from SharedKernel references.
/// </summary>
public abstract class DomainEvent
{
    public int Version { get; set; } = 1;
    public string SchemaVersion { get; set; } = "v1";
    public string? CorrelationId { get; set; }
    public Guid EventId { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
