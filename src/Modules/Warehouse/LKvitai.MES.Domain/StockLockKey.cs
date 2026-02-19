using System.Security.Cryptography;
using System.Text;

namespace LKvitai.MES.Domain;

/// <summary>
/// Canonical derivation of the PostgreSQL advisory lock key for a stock location.
///
/// [HOTFIX CRIT-01] All balance-affecting operations on a (warehouseId, location, sku)
/// must acquire pg_advisory_xact_lock(StockLockKey.ForLocation(...)) before reading
/// balance and appending events. This serializes:
///   - StartPicking (HARD lock acquisition — reads balance + hardLocked sum)
///   - All outbound/balance-decreasing movements (PICK, TRANSFER, DISPATCH, ADJUSTMENT_OUT)
///
/// Without this serialization, a concurrent outbound movement could reduce the balance
/// between StartPicking's balance read and its PickingStartedEvent commit, causing
/// hardLockedSum > balance (invariant violation).
///
/// Uses SHA-256 of "stock-lock:{warehouseId}:{location}:{sku}" truncated to Int64.
/// Deterministic: same inputs always produce the same lock key.
/// </summary>
public static class StockLockKey
{
    private const string Prefix = "stock-lock";

    /// <summary>
    /// Computes the 64-bit advisory lock key for a stock location.
    /// </summary>
    /// <param name="warehouseId">Warehouse identifier (e.g., "WH1")</param>
    /// <param name="location">Physical location code (e.g., "LOC-A")</param>
    /// <param name="sku">Stock Keeping Unit (e.g., "SKU-001")</param>
    /// <returns>Deterministic Int64 lock key for pg_advisory_xact_lock.</returns>
    public static long ForLocation(string warehouseId, string location, string sku)
    {
        ArgumentNullException.ThrowIfNull(warehouseId);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(sku);

        var combined = $"{Prefix}:{warehouseId}:{location}:{sku}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
        return BitConverter.ToInt64(hash, 0);
    }

    /// <summary>
    /// Returns the sorted, deduplicated lock keys for a set of (warehouseId, location, sku) tuples.
    /// Sorting prevents deadlocks when acquiring multiple locks.
    /// </summary>
    public static long[] ForLocations(IEnumerable<(string WarehouseId, string Location, string SKU)> locations)
    {
        return locations
            .Select(t => ForLocation(t.WarehouseId, t.Location, t.SKU))
            .Distinct()
            .OrderBy(k => k)
            .ToArray();
    }
}
