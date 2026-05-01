namespace LKvitai.MES.Modules.Sales.Contracts.Orders;

/// <summary>
/// Visual grouping of a parent product with its accessory lines. Renders as a
/// <c>group-row</c> banner followed by one or more <see cref="OrderItemDto"/> rows.
/// </summary>
public sealed record OrderItemGroupDto(
    string Label,
    IReadOnlyList<OrderItemDto> Lines);
