namespace LKvitai.MES.Modules.Sales.Contracts.Orders;

/// <summary>
/// Employee assignment row in the order details Employees table. Carries the
/// semantic <see cref="DutyCode"/> (sales / prod / inst / …) — the WebUI maps it
/// to the <c>duty--*</c> color dot class.
/// </summary>
public sealed record OrderEmployeeDto(
    string Name,
    string Initials,
    string DutyCode,
    string DutyLabel,
    DateOnly? ServiceDate,
    DateOnly? AcquaintanceDate,
    int OrderQty,
    int ItemQty,
    decimal Amount);
