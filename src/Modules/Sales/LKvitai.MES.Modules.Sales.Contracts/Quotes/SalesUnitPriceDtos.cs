namespace LKvitai.MES.Modules.Sales.Contracts.Quotes;

/// <summary>Legacy signed IDs, dimensions in mm and the order's date (not lookup time).</summary>
public sealed record SalesUnitPriceQuery(
    int TypeId,
    int ComponentId,
    int WidthMm,
    int HeightMm,
    decimal Quantity,
    DateOnly OrderDate);

public enum SalesUnitPriceStatus
{
    Found,
    Missing,
    Ambiguous,
    InvalidReference,
    InvalidPriceData,
}

/// <summary>
/// Read-only unit-price lookup, not a commercial quote, reservation, or save approval.
/// Money values retain SQL money precision. UnitPrice is supplied only for Found.
/// </summary>
public sealed record SalesUnitPriceResult(
    SalesUnitPriceStatus Status,
    decimal? UnitPrice,
    long MatchingRows,
    int DistinctPrices,
    decimal? MinimumMatchedPrice,
    decimal? MaximumMatchedPrice);
