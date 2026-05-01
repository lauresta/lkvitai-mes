namespace LKvitai.MES.Modules.Sales.Contracts.Orders;

/// <summary>
/// Operator (sales clerk) who last touched the order, plus the timestamp shown
/// under the details title (e.g. "Rūta Markevičienė · 2026-04-29 09:42:17").
/// </summary>
public sealed record OrderOperatorDto(
    string Name,
    DateTime At);
