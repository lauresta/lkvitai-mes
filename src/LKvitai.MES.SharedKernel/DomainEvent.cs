namespace LKvitai.MES.SharedKernel;

/// <summary>
/// Base class for all domain events
/// </summary>
public abstract class DomainEvent
{
    public int Version { get; set; } = 1;
    public Guid EventId { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
