namespace LKvitai.MES.Modules.Warehouse.Domain;

/// <summary>
/// Single source of truth for StockLedger event stream identity.
/// Per ADR-001: Uses (warehouseId, location, sku) partitioning.
/// Stream ID format: stock-ledger:{warehouseId}:{location}:{sku}
///
/// All code interacting with StockLedger streams MUST use this helper.
/// Direct string construction of stream IDs is prohibited.
/// </summary>
public static class StockLedgerStreamId
{
    private const string Prefix = "stock-ledger";
    private const char Separator = ':';

    /// <summary>
    /// Generates the stream ID for a StockLedger scoped to (warehouseId, location, sku).
    /// </summary>
    /// <param name="warehouseId">Warehouse identifier (e.g., "WH1")</param>
    /// <param name="location">Physical location code (e.g., "LOC-A")</param>
    /// <param name="sku">Stock Keeping Unit (e.g., "SKU-001")</param>
    /// <returns>Stream ID in format "stock-ledger:{warehouseId}:{location}:{sku}"</returns>
    public static string For(string warehouseId, string location, string sku)
    {
        if (string.IsNullOrWhiteSpace(warehouseId))
            throw new ArgumentException("Warehouse ID cannot be null or empty", nameof(warehouseId));
        if (string.IsNullOrWhiteSpace(location))
            throw new ArgumentException("Location cannot be null or empty", nameof(location));
        if (string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("SKU cannot be null or empty", nameof(sku));

        return $"{Prefix}{Separator}{warehouseId}{Separator}{location}{Separator}{sku}";
    }

    /// <summary>
    /// Parses a StockLedger stream ID into its constituent parts.
    /// </summary>
    /// <param name="streamId">The stream ID string.</param>
    /// <returns>A tuple of (warehouseId, location, sku).</returns>
    /// <exception cref="ArgumentException">Thrown if the stream ID format is invalid.</exception>
    public static (string WarehouseId, string Location, string Sku) Parse(string streamId)
    {
        if (string.IsNullOrWhiteSpace(streamId))
            throw new ArgumentException("Stream ID cannot be null or empty", nameof(streamId));

        // Split only on first 3 separators to support values containing ':'
        var parts = streamId.Split(Separator, 4);

        if (parts.Length != 4 || parts[0] != Prefix)
            throw new ArgumentException(
                $"Invalid StockLedger stream ID: '{streamId}'. " +
                $"Expected format: '{Prefix}:{{warehouseId}}:{{location}}:{{sku}}'",
                nameof(streamId));

        return (parts[1], parts[2], parts[3]);
    }
}
