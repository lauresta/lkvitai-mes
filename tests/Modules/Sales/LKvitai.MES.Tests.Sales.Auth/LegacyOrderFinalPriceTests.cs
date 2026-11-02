using LKvitai.MES.Modules.Sales.Application.OrderEntry;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using Xunit;
namespace LKvitai.MES.Tests.Sales.Auth;
public sealed class LegacyOrderFinalPriceTests
{
    [Theory]
    [InlineData(110,100,20,10)]
    [InlineData(130,100,20,-10)]
    [InlineData(10,3,0,-233.3333)]
    [InlineData(120,100,20,0)]
    public void Fixed_final_price_uses_discountable_and_non_discountable_groups(decimal desired,decimal discountable,decimal nonDiscountable,decimal percentage)
        => Assert.Equal(percentage,LegacyOrderFinalPrice.Discount(desired,discountable,nonDiscountable));
    [Theory]
    [InlineData(0,0,0)]
    [InlineData(-1,100,0)]
    [InlineData(10.00001,100,0)]
    [InlineData(5,100,20)]
    public void Impossible_or_nonstorable_fixed_prices_are_explicit_validation(decimal desired,decimal discountable,decimal nonDiscountable)
        => Assert.Throws<OrderEntryException>(()=>LegacyOrderFinalPrice.Discount(desired,discountable,nonDiscountable));
}
