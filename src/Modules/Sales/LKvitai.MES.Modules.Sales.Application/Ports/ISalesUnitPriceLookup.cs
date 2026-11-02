using LKvitai.MES.Modules.Sales.Contracts.Quotes;

namespace LKvitai.MES.Modules.Sales.Application.Ports;

/// <summary>
/// Current legacy FindPrice join semantics, without writes, sentinel prices or
/// trusted-price changes. The caller must authorize employee access separately.
/// </summary>
public interface ISalesUnitPriceLookup
{
    Task<SalesUnitPriceResult> GetUnitPriceAsync(
        SalesUnitPriceQuery query, CancellationToken cancellationToken);
}
