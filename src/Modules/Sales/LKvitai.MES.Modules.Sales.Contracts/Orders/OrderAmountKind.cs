namespace LKvitai.MES.Modules.Sales.Contracts.Orders;

/// <summary>
/// Closed set of cards in the order details Amounts grid. The WebUI maps each
/// kind to the matching <c>amount-card--*</c> modifier class and chooses the
/// money / percent formatter accordingly.
/// </summary>
public enum OrderAmountKind
{
    Defined,
    Calculated,
    Discount,
    AfterDiscount,
    Paid,
    Debt,
}
