namespace LKvitai.MES.Modules.Warehouse.Domain;

/// <summary>
/// Well-known stock movement types.
/// </summary>
public static class MovementType
{
    /// <summary>Goods entering the warehouse (inbound). FROM is external.</summary>
    public const string Receipt = "RECEIPT";

    /// <summary>Goods leaving the warehouse (outbound). TO is external.</summary>
    public const string Dispatch = "DISPATCH";

    /// <summary>Internal move between warehouse locations.</summary>
    public const string Transfer = "TRANSFER";

    /// <summary>Inventory correction adding stock.</summary>
    public const string AdjustmentIn = "ADJUSTMENT_IN";

    /// <summary>Inventory correction removing stock.</summary>
    public const string AdjustmentOut = "ADJUSTMENT_OUT";

    /// <summary>Pick movement — removing stock for production/order fulfillment.</summary>
    public const string Pick = "PICK";

    /// <summary>
    /// Returns true if the movement type is inbound (no FROM-balance check required).
    /// </summary>
    public static bool IsInbound(string movementType)
        => string.Equals(movementType, Receipt, StringComparison.OrdinalIgnoreCase)
        || string.Equals(movementType, AdjustmentIn, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// [HOTFIX CRIT-01] Returns true if the movement type decreases balance at the FROM location.
    /// These movements MUST acquire StockLockKey advisory lock to serialize with StartPicking.
    /// </summary>
    public static bool IsBalanceDecreasing(string movementType)
        => !IsInbound(movementType)
        && !string.IsNullOrEmpty(movementType);
}
