using LKvitai.MES.Modules.Finance.Api.Incomes;

namespace LKvitai.MES.Tests.Finance.Unit;

/// <summary>Small fictional legacy data set for the incomes report tests.</summary>
internal static class IncomesTestData
{
    public static readonly IncomesBranch Vilnius = new(4, "VLN", "Vilnius");
    public static readonly IncomesBranch Kaunas = new(2, "KNS", "Kaunas");
    public static readonly IncomesBranch Senukai = new(11, "SEN", "SENUKAI");
    public static IReadOnlyList<IncomesBranch> Branches { get; } = [Vilnius, Kaunas, Senukai];

    public static DateOnly D(int month, int day, int year = 2026) => new(year, month, day);

    private static int _id;

    public static IncomesRecord Payment(DateOnly date, IncomesBranch branch, string method, decimal amount,
        string? employee = "Jonas Jonaitis", string? note = null, int? bankNr = null, string? ppk = null) =>
        new($"M{++_id}", date, date.ToDateTime(new TimeOnly(10, 0)), $"VV-{_id}-26", branch.Id, "VV", method, amount, employee, note,
            PpkNo: ppk, BankTransferNo: bankNr);

    public static IncomesRecord Order(DateOnly date, IncomesBranch branch, decimal value, bool cancelled = false,
        decimal discount = 0, decimal paid = 0, string? customer = "Kaunas - mažmena", string? consultant = "Rasa Rasaitė") =>
        new($"{2148190000 + ++_id}", date, date.ToDateTime(new TimeOnly(9, 0)), $"KT-{_id}-26", branch.Id, "KT", null, value,
            consultant, null, Customer: customer, DiscountPct: discount, PaidEur: paid, Cancelled: cancelled);

    public static IncomesQuery October(IncomesDataset dataset = IncomesDataset.Money) => new()
    {
        From = D(10, 1),
        To = D(10, 31),
        Dataset = dataset,
    };

    public static IncomesSources Sources(IReadOnlyList<IncomesRecord> current, IReadOnlyList<IncomesRecord>? previous = null,
        IReadOnlyList<IncomesRecord>? lastYear = null, IReadOnlyList<IncomesRecord>? payments = null,
        IReadOnlyList<IncomesRecord>? orders = null) =>
        new(Branches, current, previous ?? [], lastYear ?? [], payments ?? current, orders ?? []);
}
