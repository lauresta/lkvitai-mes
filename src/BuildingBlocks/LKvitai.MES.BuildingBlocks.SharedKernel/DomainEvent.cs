namespace LKvitai.MES.SharedKernel;

/// <summary>
/// Base class for all domain events
/// </summary>
public abstract class DomainEvent
{
    public int Version { get; set; } = 1;
    public string SchemaVersion { get; set; } = "v1";
    public string? CorrelationId { get; set; } = CorrelationContext.Current;
    public Guid EventId { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
