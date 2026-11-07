using System.Globalization;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;

public static class Money
{
    public static string Format(decimal? value)
        => value.HasValue ? value.Value.ToString("0.00", CultureInfo.InvariantCulture) + " €" : "–";
}
