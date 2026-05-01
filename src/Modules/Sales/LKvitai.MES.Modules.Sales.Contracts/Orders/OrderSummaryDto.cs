namespace LKvitai.MES.Modules.Sales.Contracts.Orders;

/// <summary>
/// Row-level projection of a sales order for the orders list. Carries raw values
/// (decimals, dates, flags, semantic codes) — never CSS class names. The WebUI
/// derives chip / debt-tier classes and locale formatting from these fields.
/// </summary>
/// <param name="Id">Stable internal identifier (legacy <c>weblb_Order.Id</c> — BIGINT, mapped as <see cref="long"/>).</param>
/// <param name="Number">Public order number, e.g. <c>KVT-240518-018</c>.</param>
/// <param name="Date">Order date (no time component on the list).</param>
/// <param name="Price">Total order price including VAT.</param>
/// <param name="Debt">Outstanding debt at the time of the query.</param>
/// <param name="IsOverdue"><c>true</c> when <see cref="Debt"/> is past its due date — drives the danger debt tier.</param>
/// <param name="Customer">Customer display name (single line; no second meta line).</param>
/// <param name="HasDebt">Customer flag — render <c>€</c> glyph in the Flags column.</param>
/// <param name="IsVip">Customer flag — render <c>★</c> glyph in the Flags column.</param>
/// <param name="HasNote">Customer flag — render <c>!</c> glyph in the Flags column.</param>
/// <param name="Status">Localized status label as displayed (e.g. Lithuanian "Įvestas").</param>
/// <param name="StatusCode">Stable semantic code from <see cref="OrderStatusCodes"/>.</param>
/// <param name="Store">Owning store / branch (e.g. Vilnius).</param>
/// <param name="Address">Customer or delivery address shown in the list.</param>
/// <param name="ProductsSearch">
/// Hidden, non-displayed haystack of product / accessory / color / note text aggregated
/// across the order's line items. Mirrors the legacy <c>weblb_Order.ProductsSearch</c>
/// column so the toolbar search input ("/" key) matches order codes printed on items
/// (e.g. <c>R120</c>) without forcing the user to scroll through item lines. Optional
/// — adapters that have not populated it leave this <c>null</c> and the WebUI just
/// matches on <c>Number / Customer / Address</c>.
/// </param>
public sealed record OrderSummaryDto(
    long Id,
    string Number,
    DateOnly Date,
    decimal Price,
    decimal Debt,
    bool IsOverdue,
    string Customer,
    bool HasDebt,
    bool IsVip,
    bool HasNote,
    string Status,
    string StatusCode,
    string Store,
    string Address,
    string? ProductsSearch = null);
