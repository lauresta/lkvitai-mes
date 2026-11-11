using System.Globalization;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;

public static class Money
{
    private static CultureInfo DisplayCulture
    {
        get
        {
            var culture = (CultureInfo)CultureInfo.CurrentUICulture.Clone();
            culture.NumberFormat.NegativeSign = "-";
            if (culture.TwoLetterISOLanguageName == "lt")
                culture.NumberFormat.NumberGroupSeparator = " ";
            return culture;
        }
    }

    public static string Format(decimal? value)
        => value.HasValue
            ? decimal.Round(value.Value, 2, MidpointRounding.AwayFromZero).ToString("#,##0.00", DisplayCulture) + " €"
            : "–";

    public static string FormatUnit(decimal? value)
        => value.HasValue
            ? decimal.Round(value.Value, 4, MidpointRounding.AwayFromZero).ToString("#,##0.00##", DisplayCulture) + " €"
            : "–";
}
