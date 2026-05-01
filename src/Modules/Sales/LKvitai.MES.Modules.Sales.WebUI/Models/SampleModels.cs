namespace LKvitai.MES.Modules.Sales.WebUI;

/// <summary>S-0 sample view models — replaced by Sales.Contracts DTOs in S-1.</summary>

public record OrderRow(
    long Id,
    string Number,
    DateOnly Date,
    decimal Price,
    decimal Debt,
    string Customer,
    string Status,
    string Store,
    string Address,
    bool HasDebt,
    bool IsVip,
    bool HasNote);

public record OrderItemLine(
    string Title,
    string? Side,
    string? Color,
    string? WidthHeight,
    string? Notes,
    int Quantity,
    decimal Price,
    decimal Amount,
    bool IsAccessory);

public record OrderAmountCard(
    string Label,
    string Value,
    string CssModifier);

public record OrderEmployeeLine(
    string Initials,
    string FullName,
    string Duty,
    string DutyClass,
    string ServiceDate,
    decimal Amount);
