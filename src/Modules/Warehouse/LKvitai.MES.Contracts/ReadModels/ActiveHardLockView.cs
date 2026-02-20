namespace LKvitai.MES.Contracts.ReadModels;

/// <summary>
/// Read model for active HARD locks.
/// Flat table keyed by (reservationId, location, sku).
///
/// Lives in Contracts so both Projections (writer) and Infrastructure (reader)
/// can reference the type without Infrastructure depending on Projections.
/// </summary>
public class ActiveHardLockView
{
    /// <summary>
    /// Composite key: "{reservationId}:{location}:{sku}"
    /// </summary>
    public string Id { get; set; } = string.Empty;

    public string WarehouseId { get; set; } = string.Empty;
    public Guid ReservationId { get; set; }
    public string Location { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public decimal HardLockedQty { get; set; }
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Computes the deterministic document Id for a hard lock row.
    /// </summary>
    public static string ComputeId(Guid reservationId, string location, string sku)
        => $"{reservationId}:{location}:{sku}";
}
