namespace LKvitai.MES.Modules.Warehouse.Application.Ports;

/// <summary>
/// Port for querying the ActiveHardLocks read model.
/// Application layer owns this interface; Infrastructure provides the Marten implementation.
/// [MITIGATION R-4] Efficient HARD lock conflict detection for StartPicking.
/// </summary>
public interface IActiveHardLocksRepository
{
    /// <summary>
    /// Returns the SUM of hard-locked quantity for the given (location, sku).
    /// This is a read-only query (does NOT acquire locks).
    /// </summary>
    Task<decimal> SumHardLockedQtyAsync(
        string warehouseId, string location, string sku, CancellationToken ct);

    /// <summary>
    /// Returns all active hard lock entries.
    /// Used by consistency checks (e.g. OrphanHardLockCheck).
    /// </summary>
    Task<IReadOnlyList<ActiveHardLockDto>> GetAllActiveLocksAsync(CancellationToken ct);
}

/// <summary>
/// Lightweight DTO for active hard lock entries used in consistency checks.
/// </summary>
public record ActiveHardLockDto
{
    public Guid ReservationId { get; init; }
    public string WarehouseId { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string SKU { get; init; } = string.Empty;
    public decimal HardLockedQty { get; init; }
}
