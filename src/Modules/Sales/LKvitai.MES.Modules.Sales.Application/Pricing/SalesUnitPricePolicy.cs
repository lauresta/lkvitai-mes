using LKvitai.MES.Modules.Sales.Contracts.Quotes;

namespace LKvitai.MES.Modules.Sales.Application.Pricing;

/// <summary>Explicit outcomes for the legacy unordered TOP(1) lookup.</summary>
public static class SalesUnitPricePolicy
{
    public static void Validate(SalesUnitPriceQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        // IDs may be negative. No demo 200-mm lower bound or invented maximum.
        if (query.WidthMm <= 0) throw new ArgumentOutOfRangeException(nameof(query.WidthMm));
        if (query.HeightMm <= 0) throw new ArgumentOutOfRangeException(nameof(query.HeightMm));
        if (query.Quantity <= 0) throw new ArgumentOutOfRangeException(nameof(query.Quantity));
        if (query.OrderDate < new DateOnly(1753, 1, 1))
            throw new ArgumentOutOfRangeException(nameof(query.OrderDate));
    }

    public static SalesUnitPriceResult Resolve(
        bool typeExists, bool componentExists, long matchingRows,
        int distinctPrices, long nullPriceRows, decimal? minimumPrice, decimal? maximumPrice)
    {
        // A read failure/malformed aggregate must never masquerade as price zero.
        if (matchingRows < 0 || distinctPrices < 0 || nullPriceRows < 0
            || nullPriceRows > matchingRows || distinctPrices > matchingRows)
            throw new ArgumentException("Invalid price aggregate.");
        var status = !typeExists || !componentExists ? SalesUnitPriceStatus.InvalidReference
            : matchingRows == 0 ? SalesUnitPriceStatus.Missing
            : nullPriceRows != 0 || distinctPrices == 0 || minimumPrice is null || maximumPrice is null
                ? SalesUnitPriceStatus.InvalidPriceData
            : distinctPrices > 1 ? SalesUnitPriceStatus.Ambiguous
            : minimumPrice != maximumPrice ? SalesUnitPriceStatus.InvalidPriceData
            : SalesUnitPriceStatus.Found;
        return new SalesUnitPriceResult(status,
            status == SalesUnitPriceStatus.Found ? minimumPrice : null,
            matchingRows, distinctPrices, minimumPrice, maximumPrice);
    }
}
