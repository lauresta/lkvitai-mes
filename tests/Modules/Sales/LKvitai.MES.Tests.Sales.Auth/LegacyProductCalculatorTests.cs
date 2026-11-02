using LKvitai.MES.Modules.Sales.Application.OrderEntry;
using Xunit;
namespace LKvitai.MES.Tests.Sales.Auth;
public sealed class LegacyProductCalculatorTests
{
    private static readonly IReadOnlyDictionary<string,double> Constants=new Dictionary<string,double>
    { ["MaxStep127"]=114,["MaxStep89"]=79,["EdgeFix127_AB"]=139,["EdgeFix89_AB"]=101,["EdgeFix127_CDE"]=124,["EdgeFix89_CDE"]=86 };
    private static string? CalculateFormula(string formula,int width=1000)
    {
        var formulas=new string?[12];formulas[0]=formula;
        return LegacyProductCalculator.Calculate(width,1500,2,2328,"D",formulas,new string?[12],Constants,true)[0];
    }
    [Theory]
    [InlineData("CarrierQnt127AB(Width)","9")]
    [InlineData("CarrierQnt127CDE(Width)","10")]
    [InlineData("CarrierQnt89AB(Width)","13")]
    [InlineData("CarrierQnt89CDE(Width)","14")]
    [InlineData("CorrectorsQnt127AB(Width)","10")]
    [InlineData("CorrectorsQnt127CDE(Width)","28")]
    [InlineData("CorrectorsQnt89AB(Width)","9")]
    [InlineData("CorrectorsQnt89CDE(Width)","20")]
    [InlineData("Round(2.5,0)","2")]
    [InlineData("(Height/1000)*(Width/1000)*quantity","3")]
    public void Current_function_families_use_live_constant_contract(string formula,string expected)
        =>Assert.Equal(expected,CalculateFormula(formula));

    [Fact]
    public void Dimension_recalculation_overwrites_formula_slots_but_preserves_other_manual_slots()
    {
        var formulas=new string?[12];formulas[0]="Width-45";formulas[11]="((Width-45)/1000)*Quantity";
        var values=new string?[12];values[0]="manual";values[1]="text value";values[11]="4.5";
        var preserved=LegacyProductCalculator.Calculate(800,1500,2,2328,"D",formulas,values,Constants,false);
        Assert.Equal("manual",preserved[0]);Assert.Equal("4.5",preserved[11]);
        var updated=LegacyProductCalculator.Calculate(800,1500,2,2328,"D",formulas,values,Constants,true);
        Assert.Equal("755",updated[0]);Assert.Equal("text value",updated[1]);Assert.Equal("1.51",updated[11]);
    }
    [Fact]
    public void Unsupported_or_failed_formula_is_field_error_instead_of_zero()
    {
        var bad=Assert.Throws<LegacyProductRuleException>(()=>CalculateFormula("UnknownLegacyToken"));
        Assert.Equal("PPP01",bad.Field);
        Assert.Throws<LegacyProductRuleException>(()=>CalculateFormula("CorrectorsQnt127AB(Width)",30));
        Assert.Throws<LegacyProductRuleException>(()=>CalculateFormula("EXEC('malicious')"));
        var formulas=new string?[12];formulas[0]="CarrierQnt127AB(Width)";
        Assert.Throws<LegacyProductRuleException>(()=>LegacyProductCalculator.Calculate(800,1500,1,2328,"D",formulas,new string?[12],new Dictionary<string,double>(),true));
    }
    [Fact]
    public void Type_capability_checks_syntax_without_a_sample_dimension_or_constants()
    {
        var formulas=new string?[12];
        formulas[0]="CorrectorsQnt127AB(Width)/(Height-1500)";
        formulas[1]="Round(Colour/ControlType,Quantity)";
        Assert.Null(LegacyProductCalculator.FindUnsupportedReason(formulas));
        formulas[1]="Nera";
        Assert.Null(LegacyProductCalculator.FindUnsupportedReason(formulas));
        formulas[1]="NeraSuffix";
        Assert.Contains("PPP02",LegacyProductCalculator.FindUnsupportedReason(formulas));
        formulas[1]="ArbitraryFunction(Width)";
        Assert.Contains("Unsupported formula function",LegacyProductCalculator.FindUnsupportedReason(formulas));
        formulas[1]="Width+";
        Assert.NotNull(LegacyProductCalculator.FindUnsupportedReason(formulas));
    }
    [Theory]
    [InlineData("Nera")]
    [InlineData("nera")]
    [InlineData("NERA")]
    [InlineData("  neRa  ")]
    public void Owner_confirmed_standalone_nera_formula_returns_lowercase_text(string formula)
    {
        var formulas=new string?[12];formulas[1]=formula;
        Assert.Null(LegacyProductCalculator.FindUnsupportedReason(formulas));
        var values=LegacyProductCalculator.Calculate(800,1500,1,null,null,formulas,new string?[12],Constants,true);
        Assert.Equal("nera",values[1]);
    }
    [Theory]
    [InlineData("Nera+1")]
    [InlineData("Nera(1)")]
    [InlineData("AnotherIdentifier")]
    public void Text_sentinel_does_not_enable_identifier_or_function_execution(string formula)
    {
        var formulas=new string?[12];formulas[1]=formula;
        Assert.NotNull(LegacyProductCalculator.FindUnsupportedReason(formulas));
        Assert.Throws<LegacyProductRuleException>(()=>LegacyProductCalculator.Calculate(800,1500,1,null,null,formulas,new string?[12],Constants,true));
    }
    [Fact]
    public void Text_sentinel_is_not_valid_for_numeric_PPP12()
    {
        var formulas=new string?[12];formulas[11]="Nera";
        Assert.Contains("PPP12",LegacyProductCalculator.FindUnsupportedReason(formulas));
        var failure=Assert.Throws<LegacyProductRuleException>(()=>LegacyProductCalculator.Calculate(800,1500,1,null,null,formulas,new string?[12],Constants,true));
        Assert.Equal("PPP12",failure.Field);
    }
    [Fact]
    public void PPP12_is_sql_scale_three_while_text_slots_keep_their_storage_limit()
    {
        var formulas=new string?[12];formulas[11]="1.2345";
        var values=LegacyProductCalculator.Calculate(800,1500,1,2328,"D",formulas,new string?[12],Constants,true);
        Assert.Equal("1.235",values[11]);
        var manual=new string?[12];manual[0]=new string('x',16);
        Assert.Throws<LegacyProductRuleException>(()=>LegacyProductCalculator.Calculate(800,1500,1,2328,"D",new string?[12],manual,Constants,false));
        var blankNullable=new string?[12];blankNullable[11]="";
        var normalized=LegacyProductCalculator.Calculate(0,0,0.5m,0,null,new string?[12],blankNullable,Constants,true);
        Assert.Null(normalized[11]);
    }
    [Fact]
    public void Stock_comparison_excludes_current_reservation_and_keeps_strict_equality_rule()
    {
        Assert.Null(LegacyProductCalculator.CheckStock(1m,2m,2m,3));
        Assert.Throws<LegacyProductRuleException>(()=>LegacyProductCalculator.CheckStock(0m,2m,2m,3));
        Assert.NotNull(LegacyProductCalculator.CheckStock(0m,0m,1m,2));
        Assert.Null(LegacyProductCalculator.CheckStock(0m,0m,1m,1));
        Assert.Throws<LegacyProductRuleException>(()=>LegacyProductCalculator.CheckStock(0m,0m,1m,null));
        Assert.Null(LegacyProductCalculator.CheckStock(100m,0m,1m,null));
        Assert.Null(LegacyProductCalculator.CheckStock(0m,0m,1m,0));
        Assert.Null(LegacyProductCalculator.CheckStock(0m,0m,1m,-1));
    }
    [Theory]
    [InlineData(0,0,17,1)]
    [InlineData(0,0,17,2)]
    [InlineData(0,0,17,3)]
    [InlineData(0,2,2,3)]
    [InlineData(1,2,2,3)]
    [InlineData(-1,0,1,3)]
    [InlineData(0,0,0,2)]
    [InlineData(0,0,1,0)]
    [InlineData(0,0,1,-1)]
    public void Preview_and_save_agree_on_shortage_policy(int free,int own,int required,int action)
    {
        var preview=LegacyProductCalculator.PreviewStock(free,own,required,action);
        if(preview.Disposition=="block")
        {
            var error=Assert.Throws<LegacyProductRuleException>(()=>LegacyProductCalculator.CheckStock(free,own,required,action,"m"));
            Assert.Equal("INSUFFICIENT_MATERIAL",error.Code);
            Assert.Equal("colourId",error.Field);
        }
        else
        {
            var warning=LegacyProductCalculator.CheckStock(free,own,required,action,"m");
            Assert.Equal(preview.Disposition=="warn",warning is not null);
        }
    }
    [Fact]
    public void Shortage_error_reports_available_for_this_line_and_required_quantities_with_units()
    {
        var error=Assert.Throws<LegacyProductRuleException>(()=>LegacyProductCalculator.CheckStock(-2.5m,3m,17m,3,"m"));
        Assert.Contains("Required 17 m; available 0.5 m.",error.Message);
        Assert.Contains("Product not saved",error.Message);
        Assert.Contains("Choose another material",error.Message);
        var warning=LegacyProductCalculator.CheckStock(2m,3m,5m,2,"m");
        Assert.Contains("Required 5 m; available 5 m.",warning);
        Assert.Contains("greater than",warning);
        Assert.Contains("allowed with a warning",warning);
    }
    [Fact]
    public void Accessory_suggestions_use_metres_and_allow_fractional_quantities()
    {
        Assert.Equal(1.6m,LegacyProductCalculator.SuggestAccessoryQuantity(-1789163110,800,1500,2));
        Assert.Equal(3m,LegacyProductCalculator.SuggestAccessoryQuantity(1046262104,800,1500,2));
        Assert.Equal(2.4m,LegacyProductCalculator.SuggestAccessoryQuantity(-1859919715,800,1500,2));
        Assert.Equal(2m,LegacyProductCalculator.SuggestAccessoryQuantity(1664584723,800,1500,2));
    }
    [Theory]
    [InlineData(1,2,2,3,false,"enough")]
    [InlineData(0,2,2,3,true,"block")]
    [InlineData(0,0,1,1,true,"allow")]
    [InlineData(0,0,1,2,true,"warn")]
    [InlineData(0,0,1,0,true,"allow")]
    [InlineData(0,0,1,-1,true,"allow")]
    [InlineData(0,0,1,null,true,"unknown-policy")]
    [InlineData(100,0,1,null,false,"enough")]
    [InlineData(0,0,0,2,true,"warn")]
    public void Stock_preview_reports_disposition_without_reserving_or_throwing_shortage(
        int free,int own,int required,int? action,bool isShort,string expected)
    {
        var result=LegacyProductCalculator.PreviewStock(free,own,required,action);
        Assert.Equal(isShort,result.IsShort);Assert.Equal(expected,result.Disposition);
    }
    [Fact]
    public void Stock_preview_does_not_invent_missing_or_negative_material_requirement()
    {
        var missing=LegacyProductCalculator.PreviewStock(10,0,null,3);
        Assert.Null(missing.IsShort);Assert.Equal("not-calculated",missing.Disposition);
        var invalid=LegacyProductCalculator.PreviewStock(10,0,-1,3);
        Assert.Null(invalid.IsShort);Assert.Equal("invalid-requirement",invalid.Disposition);
    }
}
