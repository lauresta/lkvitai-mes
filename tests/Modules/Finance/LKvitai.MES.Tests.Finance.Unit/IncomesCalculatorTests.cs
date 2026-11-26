using LKvitai.MES.Modules.Finance.Api.Incomes;
using Xunit;
using static LKvitai.MES.Tests.Finance.Unit.IncomesTestData;

namespace LKvitai.MES.Tests.Finance.Unit;

public class IncomesCalculatorTests
{
    private static readonly DateOnly Today = D(11, 26);

    [Fact]
    public void Sql_bounds_include_the_first_and_the_last_day()
    {
        var (from, to) = IncomesPeriod.SqlBounds(D(10, 1), D(10, 31));
        Assert.Equal(new DateTime(2026, 10, 1), from);
        Assert.Equal(new DateTime(2026, 11, 1), to);
        // Both datasets filter with ">= @from AND < @to" (the legacy window used "> from AND < to").
        Assert.Contains("l.Data >= @from AND l.Data < @to", IncomesDataSource.PaymentsSql);
        Assert.Contains("m.Data >= @from AND m.Data < @to", IncomesDataSource.PaymentsSql);
        Assert.Contains("u.UzsakymoData >= @from AND u.UzsakymoData < @to", IncomesDataSource.OrdersSql);
    }

    [Fact]
    public void Money_received_counts_payments_of_the_first_and_the_last_day()
    {
        IncomesRecord[] payments =
        [
            Payment(D(9, 30), Vilnius, "BANK", 999), Payment(D(10, 1), Vilnius, "BANK", 100),
            Payment(D(10, 31), Kaunas, "EKA", 50), Payment(D(11, 1), Kaunas, "EKA", 999),
        ];
        var summary = IncomesCalculator.Summary(October(), Sources(payments), Today);
        Assert.Equal(2, summary.Totals.Count);
        Assert.Equal(150m, summary.Totals.AmountEur);
        Assert.Equal(100m, summary.Buckets.Single(b => b.Key == "2026-10-01").AmountEur);
        Assert.Equal(50m, summary.Buckets.Single(b => b.Key == "2026-10-31").AmountEur);
        Assert.Equal(31, summary.Buckets.Count);

        var table = IncomesTable.Build(October(), Branches, payments);
        Assert.Equal(2, table.Rows.Count);
    }

    [Fact]
    public void Orders_taken_counts_orders_of_the_first_and_the_last_day()
    {
        IncomesRecord[] orders =
        [
            Order(D(9, 30), Vilnius, 999), Order(D(10, 1), Vilnius, 300), Order(D(10, 31), Kaunas, 200, cancelled: true),
            Order(D(11, 1), Kaunas, 999),
        ];
        var query = October(IncomesDataset.Orders);
        var summary = IncomesCalculator.Summary(query, Sources(orders, payments: [], orders: orders), Today);
        Assert.Equal(2, summary.Totals.Count);
        Assert.Equal(500m, summary.Totals.AmountEur);
        Assert.Equal(new IncomesCountSum(1, 200m), summary.Totals.Cancelled);
        Assert.Equal(2, IncomesTable.Build(query, Branches, orders).Rows.Count);
    }

    [Fact]
    public void Comparisons_use_the_previous_month_and_the_same_month_last_year_with_the_same_filters()
    {
        IncomesRecord[] current = [Payment(D(10, 5), Vilnius, "BANK", 1100), Payment(D(10, 6), Kaunas, "BANK", 500)];
        IncomesRecord[] previous = [Payment(D(9, 1), Vilnius, "BANK", 1000), Payment(D(9, 30), Vilnius, "EKA", -50),
            Payment(D(9, 2), Kaunas, "BANK", 400)];
        IncomesRecord[] lastYear = [Payment(D(10, 31, 2025), Vilnius, "BANK", 800)];
        var query = October() with { Branches = new HashSet<string> { "VLN" } };

        var summary = IncomesCalculator.Summary(query, Sources(current, previous, lastYear), Today);

        Assert.Equal(1100m, summary.Totals.AmountEur);
        var p = summary.Comparisons.Previous;
        Assert.Equal((D(9, 1), D(9, 30)), (p.From, p.To));
        Assert.Equal((2, 950m, 1, -50m), (p.Count, p.AmountEur, p.RefundsCount, p.RefundsEur));
        var y = summary.Comparisons.LastYear;
        Assert.Equal((D(10, 1, 2025), D(10, 31, 2025)), (y.From, y.To));
        Assert.Equal((1, 800m), (y.Count, y.AmountEur));

        var change = summary.Remarks.First();
        Assert.Equal("change", change.Kind);
        Assert.Equal("15.8", change.Values["pct"]);
        Assert.Equal("10", change.Values["month"]);
        Assert.Equal("9", change.Values["previousMonth"]);
        Assert.Equal("Spalio pajamos +15,8 % nei rugsėjį", IncomesText.Remark(change));

        var methodOnly = IncomesCalculator.Summary(October() with { Methods = new HashSet<string> { "EKA" } },
            Sources(current, previous, lastYear), Today);
        Assert.Equal((1, -50m), (methodOnly.Comparisons.Previous.Count, methodOnly.Comparisons.Previous.AmountEur));
    }

    [Theory]
    [InlineData("2026-10-01", "2026-10-31", "2026-09-01", "2026-09-30")]
    [InlineData("2026-03-01", "2026-03-31", "2026-02-01", "2026-02-28")]
    [InlineData("2026-11-01", "2026-11-26", "2026-10-01", "2026-10-26")]
    [InlineData("2026-10-01", "2026-12-31", "2026-07-01", "2026-09-30")]
    [InlineData("2026-10-01", "2026-11-26", "2026-07-01", "2026-08-26")]
    [InlineData("2026-01-01", "2026-11-26", "2025-01-01", "2025-11-26")]
    [InlineData("2026-10-10", "2026-10-19", "2026-09-30", "2026-10-09")]
    public void The_previous_period_has_the_same_length(string from, string to, string previousFrom, string previousTo)
    {
        var previous = IncomesPeriod.Previous(DateOnly.Parse(from), DateOnly.Parse(to));
        Assert.Equal((DateOnly.Parse(previousFrom), DateOnly.Parse(previousTo)), previous);
    }

    [Fact]
    public void Same_period_last_year_keeps_whole_months()
    {
        Assert.Equal((D(2, 1, 2024), D(2, 29, 2024)), IncomesPeriod.LastYear(D(2, 1, 2025), D(2, 28, 2025)));
        Assert.Equal((D(1, 1, 2025), D(11, 26, 2025)), IncomesPeriod.LastYear(D(1, 1), D(11, 26)));
    }

    [Fact]
    public void Refunds_are_negative_payments()
    {
        IncomesRecord[] payments =
        [
            Payment(D(10, 2), Vilnius, "BANK", 1000), Payment(D(10, 3), Vilnius, "KASA", -120.5m),
            Payment(D(10, 4), Kaunas, "EKA", -30), Payment(D(10, 4), Kaunas, "EKA", 70),
        ];
        var summary = IncomesCalculator.Summary(October(), Sources(payments), Today);
        Assert.Equal(new IncomesCountSum(2, -150.5m), summary.Totals.Refunds);
        Assert.Equal(919.5m, summary.Totals.AmountEur);
        var remark = summary.Remarks.Single(r => r.Kind == "refunds");
        Assert.True(remark.Drill!.Refunds);
        Assert.Equal("2 grąžinimai · −150,50 €", IncomesText.Remark(remark));
        // Chart stacks only positive amounts; the bucket total stays net.
        var day = summary.Buckets.Single(b => b.Key == "2026-10-04");
        Assert.Equal(40m, day.AmountEur);
        Assert.Equal(70m, day.PositiveByMethod["EKA"]);

        var table = IncomesTable.Build(October() with { Refunds = true }, Branches, payments);
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(-150.5m, table.Refunds);
        Assert.Equal(-150.5m, table.Sum);
    }

    [Fact]
    public void Branches_come_from_the_database_including_those_without_data()
    {
        IncomesRecord[] payments = [Payment(D(10, 2), Vilnius, "BANK", 10), Payment(D(10, 3), Kaunas, "EKA", 20)];
        var summary = IncomesCalculator.Summary(October(), Sources(payments), Today);
        Assert.Equal(["Kaunas", "SENUKAI", "Vilnius"], summary.Branches.Select(b => b.Name).ToArray());
        Assert.False(summary.Branches.Single(b => b.Code == "SEN").HasData);
        Assert.True(summary.Branches.Single(b => b.Code == "VLN").HasData);
        // The branch list query excludes only the test branch, like the Sales reports.
        Assert.Contains("FROM dbo.Zinynas_filialai f", IncomesDataSource.BranchesSql);
        Assert.Contains("<> N'TES'", IncomesDataSource.BranchesSql);
        Assert.Contains("<> N'TES'", IncomesDataSource.PaymentsSql);
        Assert.Contains("<> N'TES'", IncomesDataSource.OrdersSql);
    }

    [Fact]
    public void Matrix_and_flows_respect_header_filters()
    {
        IncomesRecord[] payments =
        [
            Payment(D(10, 2), Vilnius, "BANK", 100), Payment(D(10, 2), Vilnius, "EKA", 40),
            Payment(D(10, 3), Kaunas, "PPK", 60), Payment(D(10, 3), Kaunas, "BANK", 10),
        ];
        IncomesRecord[] orders = [Order(D(10, 2), Vilnius, 500), Order(D(10, 9), Kaunas, 80)];
        var query = October() with { Methods = new HashSet<string> { "BANK" } };
        var summary = IncomesCalculator.Summary(query, Sources(payments, orders: orders), Today);

        Assert.Equal(["BANK"], summary.Columns.Select(c => c.Key).ToArray());
        Assert.Equal(110m, summary.Matrix.Sum(c => c.AmountEur));
        Assert.Equal(2, summary.Methods.Single(m => m.Code == "BANK").Count);
        // Sold vs received always uses all methods.
        var vilnius = summary.SoldVsReceived.Single(f => f.Branch == "VLN");
        Assert.Equal((500m, 140m), (vilnius.SoldEur, vilnius.ReceivedEur));
    }

    [Fact]
    public void Orders_matrix_has_iso_week_columns_clipped_to_the_period()
    {
        IncomesRecord[] orders = [Order(D(10, 1), Vilnius, 100), Order(D(10, 14), Vilnius, 50)];
        var summary = IncomesCalculator.Summary(October(IncomesDataset.Orders), Sources(orders, orders: orders), Today);
        Assert.Equal("W40", summary.Columns[0].Key);
        Assert.Equal("10-01–04", summary.Columns[0].Label);
        Assert.Equal("W44", summary.Columns[^1].Key);
        Assert.Contains(summary.Matrix, c => c.Column == "W42" && c.AmountEur == 50);
    }

    [Fact]
    public void Table_drills_searches_sorts_and_groups()
    {
        IncomesRecord[] payments =
        [
            Payment(D(10, 2), Vilnius, "BANK", 100, employee: null, bankNr: 201114010),
            Payment(D(10, 2), Kaunas, "EKA", 300, note: "Šiaulių klientas"),
            Payment(D(10, 3), Kaunas, "PPK", 60, ppk: "RRL0118522"),
        ];
        Assert.Single(IncomesTable.Build(October() with { NoEmployee = true }, Branches, payments).Rows);
        Assert.Single(IncomesTable.Build(October() with { Search = "siauliu" }, Branches, payments).Rows);
        Assert.Single(IncomesTable.Build(October() with { Search = "201114010" }, Branches, payments).Rows);
        Assert.Single(IncomesTable.Build(October() with { Search = "rrl0118" }, Branches, payments).Rows);
        Assert.Equal(2, IncomesTable.Build(October() with { DrillBranch = "KNS" }, Branches, payments).Rows.Count);

        var byAmount = IncomesTable.Build(October() with { Sort = "amt", SortDesc = true }, Branches, payments);
        Assert.Equal([300m, 100m, 60m], byAmount.Rows.Select(r => r.Record.Amount).ToArray());

        var grouped = IncomesTable.Build(October() with { Group = "branch" }, Branches, payments);
        Assert.Equal(["KNS", "VLN"], grouped.Groups.Select(g => g.Key).ToArray());
        Assert.Equal(360m, grouped.Groups[0].SumEur);
        Assert.Equal(["KNS", "KNS", "VLN"], grouped.Rows.Select(r => r.GroupKey).ToArray());

        var page = grouped.Page(1, 1);
        Assert.Equal(3, page.Total);
        var row = Assert.Single(page.Items);
        Assert.Equal("Kaunas", row.Branch);
    }

    [Fact]
    public void Remarks_find_method_concentration_odd_days_and_missing_employees()
    {
        var payments = new List<IncomesRecord>();
        for (var day = 1; day <= 31; day++)
        {
            var date = D(10, day);
            if (IncomesPeriod.IsWeekend(date)) continue;
            payments.Add(Payment(date, Vilnius, "BANK", day == 23 ? 100 : 1000));
            payments.Add(Payment(date, Kaunas, "PPK", 100));
            payments.Add(Payment(date, Kaunas, "PPK", 100, employee: day == 6 ? null : "Ona"));
        }
        var summary = IncomesCalculator.Summary(October(), Sources(payments), Today);
        var kinds = summary.Remarks.Select(r => r.Kind).ToList();
        Assert.Contains("concentration", kinds);
        Assert.Equal("KNS", summary.Remarks.Single(r => r.Kind == "concentration").Drill!.Branch);
        var odd = summary.Remarks.Single(r => r.Kind == "oddDay");
        Assert.Equal(D(10, 23), odd.Drill!.Day);
        Assert.StartsWith("2026-10-23 (Pn): ", IncomesText.Remark(odd));
        Assert.Contains("noEmployee", kinds);
        Assert.True(summary.Remarks.Count <= IncomesCalculator.MaxRemarks);
    }

    [Fact]
    public void Query_parser_defaults_to_last_month_and_validates()
    {
        var empty = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>();
        var query = IncomesQueryParser.Parse(empty, Today);
        Assert.Equal((D(10, 1), D(10, 31)), (query.From, query.To));
        Assert.Equal(IncomesDataset.Money, query.Dataset);
        Assert.Equal(("date", true, "none", "day"), (query.Sort, query.SortDesc, query.Group, query.Bucket));

        var year = IncomesQueryParser.Parse(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["from"] = "2026-01-01", ["to"] = "2026-11-26", ["ds"] = "orders", ["branch"] = "vln,KNS", ["sort"] = "-amt",
            ["cancelled"] = "1", ["refunds"] = "1",
        }, Today);
        Assert.Equal(("month", "month"), (year.Group, year.Bucket));
        Assert.Equal(new[] { "KNS", "VLN" }, year.Branches!.Order().ToArray());
        Assert.True(year.CancelledOnly);
        Assert.False(year.Refunds);

        Assert.Throws<IncomesQueryException>(() => IncomesQueryParser.Parse(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues> { ["from"] = "2026-10-31", ["to"] = "2026-10-01" }, Today));
        Assert.Throws<IncomesQueryException>(() => IncomesQueryParser.Parse(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues> { ["method"] = "CASH" }, Today));
        Assert.Throws<IncomesQueryException>(() => IncomesQueryParser.Parse(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues> { ["from"] = "2020-01-01", ["to"] = "2026-01-01" }, Today));
    }

    [Fact]
    public void Excel_export_has_the_summary_matrix_flows_and_the_grouped_table()
    {
        IncomesRecord[] payments =
        [
            Payment(D(10, 2), Vilnius, "BANK", 100, bankNr: 201114010), Payment(D(10, 3), Kaunas, "EKA", -20),
            Payment(D(10, 3), Kaunas, "PPK", 60, ppk: "RRL1"),
        ];
        var query = October() with { Group = "branch" };
        var summary = IncomesCalculator.Summary(query, Sources(payments), Today);
        var table = IncomesTable.Build(query, Branches, payments);

        var bytes = IncomesExcel.Build(query, summary, table, Branches, "https://cikada.example", "Tester", new DateTime(2026, 11, 26, 10, 42, 0));

        using var stream = new MemoryStream(bytes);
        using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
        Assert.Equal(["Suvestinė", "Filialai × būdai", "Parduota ir gauta", "Mokėjimai"], workbook.Worksheets.Select(w => w.Name).ToArray());
        var rows = workbook.Worksheet("Mokėjimai");
        Assert.Equal("Data", rows.Cell(1, 1).GetString());
        Assert.StartsWith("SUBTOTAL(9,", rows.Cell(2, 8).FormulaA1);
        Assert.Contains("HYPERLINK(\"https://cikada.example/finance/bank-payments?nr=201114010\"", rows.CellsUsed()
            .Select(c => c.FormulaA1).First(f => f.Contains("bank-payments")));
        Assert.Equal("Iplaukos_2026-10-01_2026-10-31_gauti-pinigai.xlsx", IncomesExcel.FileName(query));
    }
}
