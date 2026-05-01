namespace LKvitai.MES.Modules.Sales.Contracts.Orders;

/// <summary>
/// Full payload for the <c>/sales/orders/{number}</c> details page: the order
/// summary fields plus operator, item groups, amounts grid and employee assignments.
/// All raw values — the WebUI handles formatting and chip / debt-tier classes.
/// </summary>
public sealed record OrderDetailsDto(
    int Id,
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
    OrderOperatorDto? Operator,
    IReadOnlyList<OrderItemGroupDto> ItemGroups,
    IReadOnlyList<OrderAmountDto> Amounts,
    IReadOnlyList<OrderEmployeeDto> Employees);
