using System.Globalization;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
namespace LKvitai.MES.Modules.Sales.Application.OrderEntry;

public sealed record LegacyConfirmationTypeContext(int TypeId,int? PriceUnitId,string? PriceInputMode,bool IsManufactured);
public static class LegacyConfirmationEligibility
{
    /// <summary>Validate stored values only. No formula evaluation, repricing or mutation.</summary>
    public static ProductConfigurationInput Validate(ProductSnapshot product,LegacyConfirmationTypeContext type)
    {
        if(product.TypeId is null||product.WidthMm is null||product.HeightMm is null||product.Quantity is null||product.UnitPrice is null)
            throw new OrderEntryException("INCOMPLETE_PRODUCT","Complete and save the product configuration before confirmation.");
        if(product.TypeId!=type.TypeId)throw new OrderEntryException("TYPE_UNAVAILABLE","Stored product type metadata does not match.");
        if(product.StateId is not(4 or 7))throw new OrderEntryException("CONFIRMATION_STATE","Only entered or stopped products can be confirmed.",409);
        if(!type.IsManufactured)throw new OrderEntryException("NOT_MANUFACTURED","Select manufactured product lines for confirmation.");
        var input=new ProductConfigurationInput(product.TypeId.Value,product.WidthMm.Value,product.HeightMm.Value,
            product.Quantity.Value,product.ColourId,product.Control,product.UnitPrice.Value,false,null,product.Parameters,false);
        LegacyProductConfigurationValidation.ValidateConfiguration(input,type.PriceUnitId,validateNote:false);
        if(!LegacyPriceEntryRules.SupportsManualUnitPriceForType(type.TypeId,type.PriceInputMode)&&product.TrustedPrice!=true)
            throw new OrderEntryException("PRICE_LIST_REQUIRED","Establish the fixed product price from the price list before confirmation.");
        try
        {
            // Empty formula slots + false recalculate validate existing typed
            // storage without evaluating formulas or changing manual PPP values.
            var parameters=LegacyProductCalculator.Calculate(input.WidthMm,input.HeightMm,input.Quantity,input.ColourId,
                input.Control,new string?[12],product.Parameters,new Dictionary<string,double>(),false);
            var material=parameters[11] is null?(decimal?)null:decimal.Parse(parameters[11]!,CultureInfo.InvariantCulture);
            LegacyMaterialSelectionRules.Validate(input.ColourId,material,false);
        }
        catch(LegacyProductRuleException ex){throw new OrderEntryException(ex.Code,ex.Field+": "+ex.Message);}
        return input;
    }
}
