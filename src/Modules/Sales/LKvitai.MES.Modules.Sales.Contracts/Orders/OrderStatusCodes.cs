namespace LKvitai.MES.Modules.Sales.Contracts.Orders;

/// <summary>
/// Stable semantic codes for sales order status. Keep in sync with the chip
/// modifier classes documented in <c>docs/ux/sales-orders-codex-rules.md</c> §4
/// (chip--entered, chip--approved, chip--inprogress, chip--made, chip--shipped,
/// chip--delivered, chip--paused, chip--cancelled). The localized label
/// (Lithuanian or otherwise) travels separately on <see cref="OrderSummaryDto.Status"/>.
/// </summary>
public static class OrderStatusCodes
{
    public const string Entered    = "entered";
    public const string Approved   = "approved";
    public const string InProgress = "inprogress";
    public const string Made       = "made";
    public const string Shipped    = "shipped";
    public const string Delivered  = "delivered";
    public const string Paused     = "paused";
    public const string Cancelled  = "cancelled";
}
