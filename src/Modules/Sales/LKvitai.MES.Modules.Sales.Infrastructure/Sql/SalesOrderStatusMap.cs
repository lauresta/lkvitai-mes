using LKvitai.MES.Modules.Sales.Contracts.Orders;

namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;

/// <summary>
/// Maps the localized (Lithuanian) status label returned by <c>dbo.weblb_Orders</c>
/// / <c>dbo.weblb_Order</c> to the stable semantic <see cref="OrderStatusCodes"/>
/// the WebUI uses to pick the chip modifier class. Comparison is case-insensitive
/// and trimmed; unknown labels fall back to <see cref="OrderStatusCodes.Entered"/>
/// (slate / neutral) so an unmapped status renders as a muted chip rather than
/// breaking the row.
/// </summary>
internal static class SalesOrderStatusMap
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Įvestas"]      = OrderStatusCodes.Entered,
        ["Patvirtintas"] = OrderStatusCodes.Approved,
        ["Gaminamas"]    = OrderStatusCodes.InProgress,
        ["Pagamintas"]   = OrderStatusCodes.Made,
        ["Filialui"]     = OrderStatusCodes.Shipped,
        ["Atiduotas"]    = OrderStatusCodes.Delivered,
        ["Sustabdytas"]  = OrderStatusCodes.Paused,
        ["Atšauktas"]    = OrderStatusCodes.Cancelled,
        // SKIRTINGAI = "different" / "various" — legacy uppercase label that
        // weblb_Orders emits for an order whose lines straddle multiple status
        // families. Mapped to a dedicated MultiStatus code so the chip can
        // render in info-blue instead of falling back to slate "Entered".
        ["SKIRTINGAI"]   = OrderStatusCodes.MultiStatus,
    };

    public static string ToStatusCode(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return OrderStatusCodes.Entered;
        }

        return Map.TryGetValue(label.Trim(), out var code)
            ? code
            : OrderStatusCodes.Entered;
    }
}
