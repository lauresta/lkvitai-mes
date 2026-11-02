using LKvitai.MES.Modules.Sales.Contracts.Orders;
using Microsoft.Extensions.Localization;

namespace LKvitai.MES.Modules.Sales.WebUI.Resources;

public static class SalesStatusText
{
    public static string Localize(IStringLocalizer<SalesResources> localizer, string code, string raw)
    {
        var name = raw.Trim().ToUpperInvariant() switch
        {
            "ĮVESTAS" or "ĮVESTAS Į PROGRAMĄ" or "ENTERED" => "Entered",
            "PATVIRTINTAS" or "PATVIRTINTAS GAMYBAI" or "APPROVED" => "Approved",
            "GAMINAMAS" or "IN PRODUCTION" => "InProgress",
            "PAGAMINTAS" or "MADE" => "Made",
            "FILIALUI" or "IŠSIŲSTAS Į FILIALĄ" or "SENT TO BRANCH" => "Shipped",
            "ATIDUOTAS" or "IŠDUOTAS KLIENTUI" or "DELIVERED" => "Delivered",
            "SUSTABDYTAS" or "PAUSED" => "Paused",
            "ANULIUOTAS" or "ATŠAUKTAS" or "CANCELLED" => "Cancelled",
            "SKIRTINGAI" or "MULTIPLE STATUSES" => "MultiStatus",
            _ => null
        };
        // Unknown nonempty native labels remain data, even when an older API
        // assigns a generic Entered code to them.
        if (name is null && !string.IsNullOrWhiteSpace(raw)) return raw;
        name ??= code switch
        {
            OrderStatusCodes.Entered => "Entered",
            OrderStatusCodes.Approved => "Approved",
            OrderStatusCodes.InProgress => "InProgress",
            OrderStatusCodes.Made => "Made",
            OrderStatusCodes.Shipped => "Shipped",
            OrderStatusCodes.Delivered => "Delivered",
            OrderStatusCodes.Paused => "Paused",
            OrderStatusCodes.Cancelled => "Cancelled",
            OrderStatusCodes.MultiStatus => "MultiStatus",
            _ => null
        };
        if (name is null) return raw;
        var text = localizer[$"Orders.Status.{name}"];
        return text.ResourceNotFound ? raw : text.Value;
    }
}
