using Microsoft.Extensions.Localization;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Resources;

public static class WarehouseStatusText
{
    public static string Get(IStringLocalizer<WarehouseResources> localizer, object? status)
    {
        var code = status?.ToString();
        if (string.IsNullOrWhiteSpace(code)) return "-";
        var localized = localizer[$"Status.{code}"];
        if (!localized.ResourceNotFound) return localized.Value;
        if (code == code.ToUpperInvariant())
        {
            var titleCase = char.ToUpperInvariant(code[0]) + code[1..].ToLowerInvariant();
            localized = localizer[$"Status.{titleCase}"];
        }
        return localized.ResourceNotFound ? code : localized.Value;
    }
}
