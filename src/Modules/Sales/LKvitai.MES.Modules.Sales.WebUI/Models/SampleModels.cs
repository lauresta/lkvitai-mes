namespace LKvitai.MES.Modules.Sales.WebUI;

/// <summary>S-0 sample view models — replaced by Sales.Contracts DTOs in S-1.</summary>

public record OrderRow(
    int    Id,
    string Number,
    string Date,
    string Price,
    string Debt,
    string DebtClass,
    string Customer,
    bool   HasDebt,
    bool   IsVip,
    bool   HasNote,
    string Status,
    string StatusChip,
    string Store,
    string Address
);

public record ItemGroup(string Label, IReadOnlyList<ItemLine> Lines);

public record ItemLine(
    string Num,
    string Name,
    string Side,
    string Color,
    string Width,
    string Height,
    string Notes,
    string Qty,
    string Price,
    string Amount,
    bool   IsAcc = false
);

public record AmountCard(string Label, string Value, string Modifier = "");

public record EmployeeLine(
    string Initials,
    string Name,
    string DutyClass,
    string DutyLabel,
    string ServiceDate,
    string AcqDate,
    string OrderQty,
    string ItemQty,
    string Amount
);
