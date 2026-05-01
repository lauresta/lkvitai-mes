namespace LKvitai.MES.Modules.Sales.Contracts.Orders;

/// <summary>
/// Single card in the Amounts grid. Either a money value (<see cref="Amount"/> populated)
/// or a percent value (<see cref="Percent"/> populated) — the WebUI picks the format
/// from <see cref="Kind"/>.
/// </summary>
/// <param name="Kind">Semantic role of the card; drives both ordering and the accent class.</param>
/// <param name="Label">Display label, possibly localized (e.g. "After discount").</param>
/// <param name="Amount">Monetary amount including VAT. Null when <see cref="Kind"/> is <see cref="OrderAmountKind.Discount"/>.</param>
/// <param name="Percent">Percent value (0..100). Populated only for <see cref="OrderAmountKind.Discount"/>.</param>
public sealed record OrderAmountDto(
    OrderAmountKind Kind,
    string Label,
    decimal? Amount,
    decimal? Percent);
