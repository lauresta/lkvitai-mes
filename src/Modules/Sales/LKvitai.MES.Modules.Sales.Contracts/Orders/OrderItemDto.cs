namespace LKvitai.MES.Modules.Sales.Contracts.Orders;

/// <summary>
/// Single line on a sales order detail Items table. Accessory rows belong to the
/// preceding non-accessory line within the same <see cref="OrderItemGroupDto"/>.
/// </summary>
/// <param name="Num">Display ordinal shown in the # column. Empty for accessory rows.</param>
/// <param name="Name">Item or accessory name (no <c>↳</c> prefix — UI adds it for accessories).</param>
/// <param name="Side">Side / placement label (Left / Right / Center / "").</param>
/// <param name="Color">Color label as configured for the line.</param>
/// <param name="Width">Width in millimetres. Null for accessory rows where it is not applicable.</param>
/// <param name="Height">Height in millimetres. Null for accessory rows where it is not applicable.</param>
/// <param name="Notes">Free-form note (room, side, install hints, …).</param>
/// <param name="Qty">Ordered quantity in the item's UoM.</param>
/// <param name="Price">Unit price including VAT.</param>
/// <param name="Amount">Line total = Qty × Price.</param>
/// <param name="IsAccessory">When <c>true</c>, the WebUI renders the row with <c>acc-row</c> styling and a <c>↳</c> prefix.</param>
public sealed record OrderItemDto(
    string Num,
    string Name,
    string Side,
    string Color,
    decimal? Width,
    decimal? Height,
    string Notes,
    decimal Qty,
    decimal Price,
    decimal Amount,
    bool IsAccessory);
