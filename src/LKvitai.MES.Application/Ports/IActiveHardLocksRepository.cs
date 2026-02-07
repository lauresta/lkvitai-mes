namespace LKvitai.MES.Application.Ports;

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
}
