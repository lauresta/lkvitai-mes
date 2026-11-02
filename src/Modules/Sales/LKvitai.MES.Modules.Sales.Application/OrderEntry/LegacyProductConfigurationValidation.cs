using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
namespace LKvitai.MES.Modules.Sales.Application.OrderEntry;

/// <summary>Shared storage/numeric rules for entry and stored confirmation input.</summary>
public static class LegacyProductConfigurationValidation
{
    public static void ValidateConfiguration(ProductConfigurationInput p,int? unit,bool validateNote=true)
    {
        if(p.WidthMm<0||p.HeightMm<0||p.Quantity<=0||p.Quantity!=decimal.Round(p.Quantity,3)||p.Quantity>999999999999999.999m)
            throw new OrderEntryException("INVALID_DIMENSIONS","Check dimensions and positive quantity (up to three decimal places).");
        if((unit is -1789163110 or -1859919715 && p.WidthMm<=0)||(unit is 1046262104 or -1859919715 && p.HeightMm<=0))
            throw new OrderEntryException("DIMENSIONS_REQUIRED","The pricing unit requires positive product dimensions.");
        if(p.Control?.Length>10)throw new OrderEntryException("CONTROL_LENGTH","Legacy control value is limited to ten characters.");
        if(validateNote&&p.Note?.Length>2048)throw new OrderEntryException("PRODUCT_NOTE_LENGTH","Product notes are limited to 2048 characters.");
        if(p.Parameters is null||p.Parameters.Count!=12)throw new OrderEntryException("PPP_SHAPE","Twelve parameter slots are required.");
        ValidateUnitPrice(p.UnitPrice);
    }
    public static void ValidateUnitPrice(decimal price)
    {
        if(price<0||price>922337203685477.5807m||decimal.Round(price,4)!=price)
            throw new OrderEntryException("INVALID_PRICE","Price must be nonnegative and use up to four decimal places.");
    }
}
