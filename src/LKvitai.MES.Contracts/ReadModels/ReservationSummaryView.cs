namespace LKvitai.MES.Contracts.ReadModels;

/// <summary>
/// Read model for reservation list queries.
/// Stored as one document per reservation stream.
/// </summary>
public class ReservationSummaryView
{
    /// <summary>
    /// Marten document identifier (reservation stream key).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    public Guid ReservationId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string Status { get; set; } = string.Empty;
    public string LockType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? PickingStartedAt { get; set; }
    public int LineCount { get; set; }
}
