namespace LKvitai.MES.Modules.Sales.Contracts.Orders;

/// <summary>
/// Distinct values for the Status and Store toolbar filters, populated from the
/// current orders data. Returned by <c>GET /api/sales/orders/filters</c> so the
/// WebUI doesn't hardcode lists that drift away from the real data — fixes the
/// S-2 bug where hardcoded city names like <c>"Vilnius"</c> never matched real
/// store labels like <c>"Vilnius - mazmena"</c>, and any new status added in
/// the legacy DB silently became unfilterable.
/// </summary>
/// <param name="Statuses">Distinct, non-empty, case-sensitive status labels as
/// they appear in the underlying data, sorted alphabetically.</param>
/// <param name="Stores">Distinct, non-empty store labels as they appear in the
/// underlying data, sorted alphabetically.</param>
public sealed record OrdersFilterOptionsDto(
    IReadOnlyList<string> Statuses,
    IReadOnlyList<string> Stores);
