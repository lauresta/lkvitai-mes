namespace LKvitai.MES.Modules.Sales.Application.OrderEntry;

/// <summary>
/// Spalva=0 is the verified legacy creation/default selection. Having category
/// colour mappings alone does not make material mandatory for a manual-priced,
/// zero-consumption line such as Smulkios detalės.
/// </summary>
public static class LegacyMaterialSelectionRules
{
    public static void Validate(int colourId, decimal? materialRequired, bool useListPrice)
    {
        if(materialRequired<0)
            throw new LegacyProductRuleException("INVALID_MATERIAL_REQUIREMENT","PPP12","Material requirement must be nonnegative.");
        if(colourId==0 && (materialRequired>0 || useListPrice))
            throw new LegacyProductRuleException("COLOUR_REQUIRED","colourId","Select a material for positive consumption or a list-price lookup.");
    }
}
