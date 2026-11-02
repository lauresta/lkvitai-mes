using LKvitai.MES.Modules.Sales.Application.OrderEntry;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using Xunit;
namespace LKvitai.MES.Tests.Sales.Auth;
public sealed class LegacyConfirmationEligibilityTests
{
    private static readonly LegacyConfirmationTypeContext FixedWidth=new(-1025240863,-1789163110,"fixed",true);
    private static ProductSnapshot Product()=>new(2152012295,-1025240863,1,"R25","Roletai",800,1500,1,6026,"6026","D",20.8526m,null,
        new string?[]{"nera",null,null,null,null,null,null,null,null,null,null,"0.755"},4,"Įvestas",true,true,true,16.68m,[],null,true,-1789163110);

    [Fact]
    public void Newly_created_line_with_null_price_cannot_be_confirmed_even_with_default_quantity()
    {
        var error=Assert.Throws<OrderEntryException>(()=>LegacyConfirmationEligibility.Validate(Product() with{WidthMm=0,HeightMm=0,Quantity=1,UnitPrice=null},FixedWidth));
        Assert.Equal("INCOMPLETE_PRODUCT",error.Code);
    }
    [Theory]
    [InlineData(-1789163110,0,1500)][InlineData(1046262104,800,0)]
    [InlineData(-1859919715,0,1500)][InlineData(-1859919715,800,0)]
    public void Pricing_unit_requires_its_own_positive_measure(int unit,int width,int height)
    {
        var error=Assert.Throws<OrderEntryException>(()=>LegacyConfirmationEligibility.Validate(Product() with{WidthMm=width,HeightMm=height},FixedWidth with{PriceUnitId=unit}));
        Assert.Equal("DIMENSIONS_REQUIRED",error.Code);
    }
    [Fact]
    public void Piece_product_can_have_zero_dimensions_and_explicit_zero_manual_price()
    {
        var p=Product() with{WidthMm=0,HeightMm=0,Quantity=0.5m,UnitPrice=0,TrustedPrice=false,ColourId=0,Parameters=new string?[12]};
        var result=LegacyConfirmationEligibility.Validate(p,FixedWidth with{PriceUnitId=1664584723,PriceInputMode="any"});
        Assert.Equal(0,result.WidthMm);Assert.Equal(0,result.HeightMm);Assert.Equal(0,result.UnitPrice);Assert.Equal(0.5m,result.Quantity);
    }
    [Fact]
    public void Stored_manual_parameters_and_unrelated_historical_note_are_not_recalculated_or_rewritten()
    {
        var p=Product() with{Note=new string('x',3000)};
        var result=LegacyConfirmationEligibility.Validate(p,FixedWidth);
        Assert.Same(p.Parameters,result.Parameters);Assert.Equal("nera",result.Parameters[0]);Assert.Equal("0.755",result.Parameters[11]);
        Assert.False(result.RecalculateParameters);Assert.Null(result.Note);Assert.Equal(3000,p.Note.Length);
    }
    [Fact]
    public void Fixed_price_requires_trust_while_actual_manual_type_exception_remains_allowed()
    {
        var error=Assert.Throws<OrderEntryException>(()=>LegacyConfirmationEligibility.Validate(Product() with{TrustedPrice=false},FixedWidth));
        Assert.Equal("PRICE_LIST_REQUIRED",error.Code);
        var exception=Product() with{TypeId=428417548,TrustedPrice=false};
        LegacyConfirmationEligibility.Validate(exception,FixedWidth with{TypeId=428417548});
    }
    [Theory]
    [InlineData("-0.001","INVALID_MATERIAL_REQUIREMENT")][InlineData("0.1","COLOUR_REQUIRED")]
    [InlineData("not-a-number","PPP_CALCULATION_FAILED")]
    public void Stored_material_must_be_valid_without_a_new_formula_evaluation(string material,string code)
    {
        var p=Product() with{ColourId=0};var values=p.Parameters.ToArray();values[11]=material;p=p with{Parameters=values};
        var error=Assert.Throws<OrderEntryException>(()=>LegacyConfirmationEligibility.Validate(p,FixedWidth));Assert.Equal(code,error.Code);
    }
    [Fact]
    public void Nonmanufactured_locked_or_mismatched_type_is_not_eligible()
    {
        Assert.Equal("NOT_MANUFACTURED",Assert.Throws<OrderEntryException>(()=>LegacyConfirmationEligibility.Validate(Product(),FixedWidth with{IsManufactured=false})).Code);
        Assert.Equal("CONFIRMATION_STATE",Assert.Throws<OrderEntryException>(()=>LegacyConfirmationEligibility.Validate(Product() with{StateId=1173102375},FixedWidth)).Code);
        Assert.Equal("TYPE_UNAVAILABLE",Assert.Throws<OrderEntryException>(()=>LegacyConfirmationEligibility.Validate(Product(),FixedWidth with{TypeId=1})).Code);
    }
}
