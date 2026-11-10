using System.Globalization;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;

public static class Money
{
    public static string Format(decimal? value)
        => value.HasValue
            ? decimal.Round(value.Value, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture) + " €"
            : "–";

    public static string FormatUnit(decimal? value)
        => value.HasValue
            ? decimal.Round(value.Value, 4, MidpointRounding.AwayFromZero).ToString("0.00##", CultureInfo.InvariantCulture) + " €"
            : "–";
}
