namespace LKvitai.MES.Modules.Warehouse.Api.Services;

/// <summary>The arithmetic of the supplier stock report, kept apart from data access so it can be tested.</summary>
public static class SupplierStockMath
{
    /// <summary>What is still expected on one purchase document line; a line received in full or over is not expected any more.</summary>
    public static decimal OpenQty(decimal expectedQty, decimal receivedQty) => Math.Max(0m, expectedQty - receivedQty);

    /// <summary>
    /// How much is missing to reach the minimum, counting what is on hand and what is already ordered.
    /// Without a minimum there is no shortage to report.
    /// </summary>
    public static decimal? Shortage(decimal? minQty, decimal onHand, decimal openInbound)
        => minQty is null ? null : Math.Max(0m, minQty.Value - (onHand + openInbound));

    /// <summary>Average quantity issued per month over the report period.</summary>
    public static decimal AveragePerMonth(decimal issued, int months)
        => months <= 0 ? 0m : decimal.Round(issued / months, 3, MidpointRounding.AwayFromZero);

    /// <summary>How a stock movement out of the warehouse counts in the report, by its destination.</summary>
    public static SupplierStockIssueKind? KindOf(string? toLocation) => toLocation?.Trim().ToUpperInvariant() switch
    {
        "PRODUCTION" => SupplierStockIssueKind.Production,
        "CUSTOMER" => SupplierStockIssueKind.Dispatch,
        "SCRAP" => SupplierStockIssueKind.WriteOff,
        _ => null
    };
}

public enum SupplierStockIssueKind
{
    Production,
    Dispatch,
    WriteOff
}
