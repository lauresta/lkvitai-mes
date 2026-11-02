using LKvitai.MES.Modules.Sales.Application.OrderEntry;
using Xunit;
namespace LKvitai.MES.Tests.Sales.Auth;
public sealed class LegacyMaterialSelectionRulesTests
{
    [Fact]
    public void Current_standalone_priedai_without_material_can_keep_legacy_default_zero_colour()
    {
        // Current SQL type2112842477 Smulkios detalės: piece1664584723, modeNULL/any,
        // all12 NPP formulasNULL. No category-label or manufactured-flag inference.
        Assert.True(LegacyPriceEntryRules.SupportsManualUnitPriceForType(2112842477,null));
        var ppp=LegacyProductCalculator.Calculate(0,0,1m,0,null,new string?[12],new string?[12],new Dictionary<string,double>(),true);
        Assert.Null(ppp[11]);
        LegacyMaterialSelectionRules.Validate(0,null,false);
        LegacyMaterialSelectionRules.Validate(0,0m,false);
    }
    [Theory]
    [InlineData(0.001,false)]
    [InlineData(0,true)]
    public void Positive_material_or_explicit_list_lookup_requires_a_selection(decimal material,bool fromList)
    {
        var error=Assert.Throws<LegacyProductRuleException>(()=>LegacyMaterialSelectionRules.Validate(0,material,fromList));
        Assert.Equal("COLOUR_REQUIRED",error.Code);
    }
    [Fact]
    public void A_default_colour_cannot_be_used_to_store_negative_material_reservations()
    {
        var error=Assert.Throws<LegacyProductRuleException>(()=>LegacyMaterialSelectionRules.Validate(0,-0.001m,false));
        Assert.Equal("INVALID_MATERIAL_REQUIREMENT",error.Code);
    }
    [Fact]
    public void Signed_nonzero_legacy_component_ids_are_not_confused_with_the_default_selection()
    {
        LegacyMaterialSelectionRules.Validate(-123,1m,true);
        // Applicability and stock policy are still checked in SQL by the writer.
    }
}
