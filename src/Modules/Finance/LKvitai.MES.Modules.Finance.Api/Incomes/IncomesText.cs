using System.Globalization;

namespace LKvitai.MES.Modules.Finance.Api.Incomes;

/// <summary>Lithuanian wording for the Excel export (the page words the same things through its resources).</summary>
public static class IncomesText
{
    private const char Nbsp = ' ';
    private const char Minus = '−';
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly string[] MonthGenitive =
        ["sausio", "vasario", "kovo", "balandžio", "gegužės", "birželio", "liepos", "rugpjūčio", "rugsėjo", "spalio", "lapkričio", "gruodžio"];
    private static readonly string[] MonthAccusative =
        ["sausį", "vasarį", "kovą", "balandį", "gegužę", "birželį", "liepą", "rugpjūtį", "rugsėjį", "spalį", "lapkritį", "gruodį"];

    /// <summary>LT money: <c>1 240,00 €</c> with non-breaking spaces and U+2212 for negatives.</summary>
    public static string Money(decimal value, int decimals = 2)
    {
        var rounded = Math.Round(Math.Abs(value), decimals, MidpointRounding.AwayFromZero);
        var text = rounded.ToString(decimals == 0 ? "#,0" : "#,0.00", Invariant).Replace(',', Nbsp).Replace('.', ',');
        return (value < 0 && rounded != 0 ? Minus.ToString() : "") + text + Nbsp + "€";
    }

    public static string Signed(decimal percent) =>
        (percent > 0.05m ? "+" : percent < -0.05m ? Minus.ToString() : "") +
        Math.Abs(Math.Round(percent, 1, MidpointRounding.AwayFromZero)).ToString("0.0", Invariant).Replace('.', ',') + Nbsp + "%";

    /// <summary>LT plural: 1, 21, 31… → one; 2–9, 22–29… → few; 0, 10–20… → many.</summary>
    public static string Plural(int count, string one, string few, string many)
    {
        var last = Math.Abs(count) % 10;
        var lastTwo = Math.Abs(count) % 100;
        if (last == 1 && lastTwo != 11) return one;
        return last is >= 2 and <= 9 && lastTwo is not (>= 12 and <= 19) ? few : many;
    }

    public static string Dataset(IncomesDataset dataset) =>
        dataset == IncomesDataset.Money ? "Gauti pinigai (pagal mokėjimo datą)" : "Priimti užsakymai (pagal užsakymo datą)";

    public static string Group(string group) => group switch
    {
        "day" => "Diena",
        "week" => "Savaitė",
        "month" => "Mėnuo",
        "branch" => "Filialas",
        "method" => "Būdas",
        "employee" => "Darbuotojas",
        "consultant" => "Konsultantas",
        _ => "nėra",
    };

    public static string Weekday(DateOnly day) => day.DayOfWeek switch
    {
        DayOfWeek.Monday => "Pr",
        DayOfWeek.Tuesday => "An",
        DayOfWeek.Wednesday => "Tr",
        DayOfWeek.Thursday => "Kt",
        DayOfWeek.Friday => "Pn",
        DayOfWeek.Saturday => "Št",
        _ => "Sk",
    };

    public static string Drill(IncomesQuery query, IReadOnlyList<IncomesBranch> branches)
    {
        var parts = new List<string>();
        if (query.Day is { } day) parts.Add($"Diena: {day:yyyy-MM-dd} ({Weekday(day)})");
        if (query.Week is { } week) parts.Add($"Savaitė {week}");
        if (query.Month is { } month) parts.Add($"Mėnuo: {month}");
        if (query.DrillBranch is { } code) parts.Add("Filialas: " + (branches.FirstOrDefault(b => b.Code == code)?.Name ?? code));
        if (query.DrillMethod is { } method) parts.Add("Būdas: " + IncomesMethods.Name(method));
        if (query.Refunds) parts.Add("Tik grąžinimai");
        if (query.NoEmployee) parts.Add("Be darbuotojo");
        if (query.CancelledOnly) parts.Add("Tik anuliuoti");
        return parts.Count == 0 ? "nėra" : string.Join(" · ", parts);
    }

    public static string Branches(IncomesQuery query, IReadOnlyList<IncomesBranch> branches) =>
        query.Branches is null ? "visi"
        : string.Join(", ", branches.Where(b => query.Branches.Contains(b.Code)).Select(b => b.Name));

    public static string Methods(IncomesQuery query) =>
        !query.IsMoney ? "netaikoma"
        : query.Methods is null ? "visi"
        : string.Join(", ", IncomesMethods.All.Where(query.Methods.Contains).Select(IncomesMethods.Name));

    /// <summary>The remark sentence, as on the page in Lithuanian.</summary>
    public static string Remark(IncomesRemark remark)
    {
        string V(string key) => remark.Values.TryGetValue(key, out var value) ? value : "";
        decimal D(string key) => decimal.TryParse(V(key), NumberStyles.Number, Invariant, out var value) ? value : 0;
        int N(string key) => (int)D(key);
        string Percent(string key) => V(key).Replace('.', ',') + Nbsp + "%";
        return remark.Kind switch
        {
            "change" => MonthIndex(V("month")) is { } m && MonthIndex(V("previousMonth")) is { } p
                ? $"{Capital(MonthGenitive[m])} pajamos {Signed(D("pct"))} nei {MonthAccusative[p]}"
                : $"Pajamos {Signed(D("pct"))} nei ankstesnį laikotarpį",
            "changeSold" => MonthIndex(V("month")) is { } m && MonthIndex(V("previousMonth")) is { } p
                ? $"{Capital(MonthAccusative[m])} parduota {Signed(D("pct"))} nei {MonthAccusative[p]}"
                : $"Parduota {Signed(D("pct"))} nei ankstesnį laikotarpį",
            "refunds" => $"{N("count")} {Plural(N("count"), "grąžinimas", "grąžinimai", "grąžinimų")} · {Money(D("amount"))}",
            "concentration" => $"{V("branch")}: {Percent("share")} mokėjimų {IncomesMethods.Name(V("method")).ToLowerInvariant()} (tinkle {Percent("network")})",
            "oddDay" => $"{V("day")} ({Weekday(DateOnly.ParseExact(V("day"), "yyyy-MM-dd", Invariant))}): {Money(D("amount"), 0)} — {Percent("pct")} mažiau nei įprasta darbo diena",
            "noEmployee" => $"{N("count")} {Plural(N("count"), "mokėjimas", "mokėjimai", "mokėjimų")} be darbuotojo",
            "allEmployees" => "Visi kasos mokėjimai turi darbuotoją",
            "cancelled" => $"{N("count")} {Plural(N("count"), "anuliuotas", "anuliuoti", "anuliuotų")} kliento prašymu · lieka {Money(D("amount"), 0)}",
            "flowRatio" => $"Gauta {Percent("pct")} parduotos sumos — srautai, ne skola",
            "discount" => $"Vid. nuolaida {Percent("average")}; didžiausia — {V("branch")} {Percent("branchAverage")}",
            _ => remark.Kind,
        };
    }

    private static int? MonthIndex(string month) =>
        int.TryParse(month, NumberStyles.None, Invariant, out var value) && value is >= 1 and <= 12 ? value - 1 : null;

    private static string Capital(string text) => text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
