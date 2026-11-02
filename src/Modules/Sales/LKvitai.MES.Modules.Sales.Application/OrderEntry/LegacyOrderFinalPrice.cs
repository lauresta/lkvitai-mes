using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
namespace LKvitai.MES.Modules.Sales.Application.OrderEntry;
public static class LegacyOrderFinalPrice
{
    public static decimal Discount(decimal desiredTotal,decimal discountable,decimal nonDiscountable)
    {
        if(desiredTotal<0||decimal.Round(desiredTotal,4)!=desiredTotal)throw new OrderEntryException("INVALID_FINAL_PRICE","Enter a nonnegative final price with up to four decimal places.");
        if(discountable<=0)throw new OrderEntryException("NO_DISCOUNTABLE_AMOUNT","No discountable amount exists for the requested final price.");
        // Current VBA computes the ratio and then CCur rounds the percentage to four places.
        var discount=decimal.Round(100-(desiredTotal-nonDiscountable)*100/discountable,4,MidpointRounding.ToEven);
        if(discount>100 || discount< -99999999.9999999999m)throw new OrderEntryException("FINAL_PRICE_RANGE","Requested final price exceeds the legacy discount range.");
        return discount;
    }
}
