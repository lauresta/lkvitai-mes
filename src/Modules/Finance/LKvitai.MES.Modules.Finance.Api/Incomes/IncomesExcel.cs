using System.Globalization;
using ClosedXML.Excel;

namespace LKvitai.MES.Modules.Finance.Api.Incomes;

/// <summary>
/// The Excel workbook of exactly what the page shows: summary (filters, KPIs with both comparisons, remarks),
/// the matrix, sold vs received, and the table with the current drill filters, search, sort and grouping.
/// </summary>
public static class IncomesExcel
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string MoneyFormat = "#,##0.00\\ \"€\";\\−#,##0.00\\ \"€\"";
    private const string DateFormat = "yyyy-mm-dd";
    private const string ChangeFormat = "+0.0%;\\−0.0%;0.0%";
    private static readonly XLColor HeaderFill = XLColor.FromHtml("#F4F5F7");
    private static readonly XLColor GroupFill = XLColor.FromHtml("#F9FAFB");

    public static string FileName(IncomesQuery query) =>
        string.Create(CultureInfo.InvariantCulture,
            $"Iplaukos_{query.From:yyyy-MM-dd}_{query.To:yyyy-MM-dd}_{(query.IsMoney ? "gauti-pinigai" : "priimti-uzsakymai")}.xlsx");

    public static string MatrixSheetName(IncomesQuery query) =>
        query.IsMoney ? "Filialai × būdai" : query.Days > IncomesPeriod.LongPeriodDays ? "Filialai × mėnesiai" : "Filialai × savaitės";

    public static string TableSheetName(IncomesQuery query) => query.IsMoney ? "Mokėjimai" : "Užsakymai";

    public static byte[] Build(IncomesQuery query, IncomesSummary summary, IncomesTable table,
        IReadOnlyList<IncomesBranch> branches, string baseUrl, string user, DateTime generated)
    {
        using var workbook = new XLWorkbook();
        Summary(workbook.AddWorksheet("Suvestinė"), query, summary, table, branches, user, generated);
        Matrix(workbook.AddWorksheet(MatrixSheetName(query)), summary);
        Flows(workbook.AddWorksheet("Parduota ir gauta"), summary);
        Table(workbook.AddWorksheet(TableSheetName(query)), query, table, baseUrl.TrimEnd('/'));
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void Summary(IXLWorksheet sheet, IncomesQuery query, IncomesSummary summary, IncomesTable table,
        IReadOnlyList<IncomesBranch> branches, string user, DateTime generated)
    {
        sheet.Cell(1, 1).Value = "Įplaukų judėjimas";
        sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        var lines = new (string Label, string Value)[]
        {
            ("Laikotarpis", $"{query.From:yyyy-MM-dd} — {query.To:yyyy-MM-dd} ({summary.Period.Days} d. · {summary.Period.Workdays} darbo d.)"),
            ("Duomenys", IncomesText.Dataset(query.Dataset)),
            ("Filialai", IncomesText.Branches(query, branches)),
            ("Būdai", IncomesText.Methods(query)),
            ("Lentelės filtrai", IncomesText.Drill(query, branches)),
            ("Paieška", query.Search.Length == 0 ? "—" : $"„{query.Search}“"),
            ("Grupavimas", IncomesText.Group(query.Group)),
            ("Lentelės eilutės", $"{table.Rows.Count} iš {summary.Totals.Count}"),
            ("Sugeneruota", $"{generated:yyyy-MM-dd HH:mm} · {user}"),
        };
        var row = 2;
        foreach (var (label, value) in lines)
        {
            sheet.Cell(row, 1).Value = label;
            sheet.Cell(row, 1).Style.Font.SetBold();
            sheet.Cell(row, 2).Value = value;
            row++;
        }

        row++;
        var previous = summary.Comparisons.Previous;
        var lastYear = summary.Comparisons.LastYear;
        string[] heads = ["Rodiklis", "Reikšmė", $"Ankstesnis {Range(previous.From, previous.To)}", "Pokytis",
            $"Praėję metai {Range(lastYear.From, lastYear.To)}", "Pokytis"];
        Header(sheet, row, heads);
        row++;
        var t = summary.Totals;
        var money = query.IsMoney;
        Kpi(sheet, row++, money ? "Gauta pinigų" : "Parduota (po nuolaidų)", t.AmountEur, previous.AmountEur, lastYear.AmountEur, true);
        Kpi(sheet, row++, money ? "Mokėjimų" : "Užsakymų", t.Count, previous.Count, lastYear.Count, false);
        Kpi(sheet, row++, money ? "Vidutinis mokėjimas" : "Vidutinis užsakymas", Average(t.AmountEur, t.Count),
            Average(previous.AmountEur, previous.Count), Average(lastYear.AmountEur, lastYear.Count), true);
        sheet.Cell(row, 1).Value = money ? "Grąžinimai" : "Anuliuota kliento prašymu";
        var extra = money ? t.Refunds : t.Cancelled;
        sheet.Cell(row, 2).Value = extra.AmountEur;
        sheet.Cell(row, 2).Style.NumberFormat.Format = MoneyFormat;
        sheet.Cell(row, 3).Value = money ? $"{extra.Count} su minuso ženklu" : $"{extra.Count} užsakymų, lieka mokėti";
        row += 2;

        sheet.Cell(row, 1).Value = "Pastabos";
        sheet.Cell(row, 1).Style.Font.SetBold();
        foreach (var remark in summary.Remarks)
            sheet.Cell(++row, 1).Value = $"{remark.Glyph}  {IncomesText.Remark(remark)}";

        sheet.Column(1).Width = 30;
        sheet.Column(2).Width = 22;
        for (var column = 3; column <= 6; column++) sheet.Column(column).Width = 22;
    }

    private static void Kpi(IXLWorksheet sheet, int row, string label, decimal value, decimal previous, decimal lastYear, bool isMoney)
    {
        sheet.Cell(row, 1).Value = label;
        var cells = new[] { (2, value), (3, previous), (5, lastYear) };
        foreach (var (column, number) in cells)
        {
            sheet.Cell(row, column).Value = number;
            sheet.Cell(row, column).Style.NumberFormat.Format = isMoney ? MoneyFormat : "#,##0";
        }
        Change(sheet.Cell(row, 4), value, previous);
        Change(sheet.Cell(row, 6), value, lastYear);
    }

    private static void Change(IXLCell cell, decimal value, decimal baseline)
    {
        if (baseline == 0) { cell.Value = "—"; return; }
        cell.Value = (value - baseline) / Math.Abs(baseline);
        cell.Style.NumberFormat.Format = ChangeFormat;
    }

    private static decimal Average(decimal amount, int count) => count == 0 ? 0 : Math.Round(amount / count, 2);

    private static string Range(DateOnly from, DateOnly to) =>
        from.Day == 1 && IncomesPeriod.IsLastDayOfMonth(to) && from.Month == to.Month && from.Year == to.Year
            ? from.ToString("yyyy-MM", CultureInfo.InvariantCulture)
            : $"{from:yyyy-MM-dd} — {to:yyyy-MM-dd}";

    private static void Matrix(IXLWorksheet sheet, IncomesSummary summary)
    {
        var columns = summary.Columns;
        string[] heads = ["Filialas", .. columns.Select(c => c.Sub is null ? c.Label : $"Sav. {c.Sub} · {c.Label}"), "Viso"];
        Header(sheet, 1, heads);
        var names = summary.Branches.ToDictionary(b => b.Code, b => b.Name);
        var rows = summary.Matrix.GroupBy(cell => cell.Branch)
            .OrderByDescending(g => g.Sum(cell => cell.AmountEur)).ToList();
        var row = 2;
        foreach (var branch in rows)
        {
            sheet.Cell(row, 1).Value = names.GetValueOrDefault(branch.Key, branch.Key);
            for (var i = 0; i < columns.Count; i++)
            {
                var cell = branch.FirstOrDefault(c => c.Column == columns[i].Key);
                if (cell is not null) sheet.Cell(row, i + 2).Value = cell.AmountEur;
            }
            sheet.Cell(row, columns.Count + 2).FormulaA1 = $"SUM({Address(row, 2)}:{Address(row, columns.Count + 1)})";
            row++;
        }
        sheet.Cell(row, 1).Value = "Viso";
        for (var column = 2; column <= columns.Count + 2; column++)
            sheet.Cell(row, column).FormulaA1 = rows.Count == 0 ? "0" : $"SUM({Address(2, column)}:{Address(row - 1, column)})";
        sheet.Row(row).Style.Font.SetBold();
        sheet.Range(2, 2, row, columns.Count + 2).Style.NumberFormat.Format = MoneyFormat;
        sheet.Column(1).Width = 24;
        for (var column = 2; column <= columns.Count + 2; column++) sheet.Column(column).Width = 18;
        sheet.SheetView.FreezeRows(1);
    }

    private static void Flows(IXLWorksheet sheet, IncomesSummary summary)
    {
        Header(sheet, 1, ["Filialas", "Parduota", "Gauta", "Gauta / Parduota"]);
        var names = summary.Branches.ToDictionary(b => b.Code, b => b.Name);
        var row = 2;
        foreach (var flow in summary.SoldVsReceived)
        {
            sheet.Cell(row, 1).Value = names.GetValueOrDefault(flow.Branch, flow.Branch);
            sheet.Cell(row, 2).Value = flow.SoldEur;
            sheet.Cell(row, 3).Value = flow.ReceivedEur;
            sheet.Cell(row, 4).FormulaA1 = $"IF(B{row}=0,\"—\",C{row}/B{row})";
            row++;
        }
        sheet.Cell(row, 1).Value = "Viso";
        sheet.Cell(row, 2).FormulaA1 = row == 2 ? "0" : $"SUM(B2:B{row - 1})";
        sheet.Cell(row, 3).FormulaA1 = row == 2 ? "0" : $"SUM(C2:C{row - 1})";
        sheet.Cell(row, 4).FormulaA1 = $"IF(B{row}=0,\"—\",C{row}/B{row})";
        sheet.Row(row).Style.Font.SetBold();
        sheet.Range(2, 2, row, 3).Style.NumberFormat.Format = MoneyFormat;
        sheet.Range(2, 4, row, 4).Style.NumberFormat.Format = "0%";
        sheet.Cell(row + 2, 1).Value = "Tai ne skola: šio laikotarpio mokėjimai dažnai apmoka ankstesnių mėnesių užsakymus.";
        sheet.Column(1).Width = 24;
        for (var column = 2; column <= 4; column++) sheet.Column(column).Width = 18;
    }

    private static void Table(IXLWorksheet sheet, IncomesQuery query, IncomesTable table, string baseUrl)
    {
        var money = query.IsMoney;
        string[] heads = money
            ? ["Data", "Užsakymas", "Filialas", "Būdas", "Dokumentas", "Pastaba", "Darbuotojas", "Suma"]
            : ["Data", "Užsakymas", "Filialas", "Konsultantas", "Klientas", "Nuolaida, %", "Apmokėta", "Būsena", "Vertė"];
        var amountColumn = heads.Length;
        Header(sheet, 1, heads);
        sheet.Outline.SummaryVLocation = XLOutlineSummaryVLocation.Top;

        var row = 2;
        var byGroup = table.Rows.ToLookup(r => r.GroupKey, StringComparer.Ordinal);
        var groups = table.Groups.Count == 0
            ? new List<(IncomesGroup? Group, IReadOnlyList<IncomesTableRow> Rows)> { (null, table.Rows) }
            : table.Groups.Select(g => ((IncomesGroup?)g, (IReadOnlyList<IncomesTableRow>)byGroup[g.Key].ToList())).ToList();
        foreach (var (group, rows) in groups)
        {
            var headerRow = row;
            if (group is not null)
            {
                sheet.Cell(row, 1).Value = $"{group.Label} · {group.Count}";
                sheet.Range(row, 1, row, amountColumn).Style.Font.SetBold().Fill.SetBackgroundColor(GroupFill);
                row++;
            }
            var first = row;
            foreach (var item in rows) Line(sheet, row++, money, item, baseUrl);
            if (group is null) continue;
            sheet.Cell(headerRow, amountColumn).FormulaA1 = rows.Count == 0
                ? "0" : $"SUBTOTAL(9,{Address(first, amountColumn)}:{Address(row - 1, amountColumn)})";
            sheet.Cell(headerRow, amountColumn).Style.NumberFormat.Format = MoneyFormat;
            if (rows.Count > 0) sheet.Rows(first, row - 1).Group();
        }
        var last = Math.Max(1, row - 1);
        sheet.Cell(row, 1).Value = "VISO";
        sheet.Cell(row, amountColumn).FormulaA1 = last < 2 ? "0" : $"SUBTOTAL(9,{Address(2, amountColumn)}:{Address(last, amountColumn)})";
        sheet.Range(row, 1, row, amountColumn).Style.Font.SetBold();
        sheet.Cell(row, amountColumn).Style.NumberFormat.Format = MoneyFormat;

        if (last >= 2)
        {
            sheet.Range(1, 1, last, amountColumn).SetAutoFilter();
            sheet.Range(2, 1, last, 1).Style.NumberFormat.Format = DateFormat;
            sheet.Range(2, amountColumn, last, amountColumn).Style.NumberFormat.Format = MoneyFormat;
            if (!money) sheet.Range(2, 7, last, 7).Style.NumberFormat.Format = MoneyFormat;
        }
        sheet.SheetView.FreezeRows(1);
        double[] widths = money ? [12, 16, 18, 12, 16, 40, 24, 14] : [12, 16, 18, 24, 34, 12, 14, 12, 14];
        for (var i = 0; i < widths.Length; i++) sheet.Column(i + 1).Width = widths[i];
    }

    private static void Line(IXLWorksheet sheet, int row, bool money, IncomesTableRow item, string baseUrl)
    {
        var r = item.Record;
        sheet.Cell(row, 1).Value = r.Date.ToDateTime(TimeOnly.MinValue);
        Link(sheet.Cell(row, 2), $"{baseUrl}/sales/orders/{Uri.EscapeDataString(r.OrderNo)}", r.OrderNo);
        sheet.Cell(row, 3).Value = item.Branch.Name;
        if (money)
        {
            sheet.Cell(row, 4).Value = IncomesMethods.Name(r.Method ?? "");
            if (r.BankTransferNo is { } nr)
                Link(sheet.Cell(row, 5), string.Create(CultureInfo.InvariantCulture, $"{baseUrl}/finance/bank-payments?nr={nr}"),
                    string.Create(CultureInfo.InvariantCulture, $"NR {nr}"));
            else
                sheet.Cell(row, 5).Value = r.PpkNo is { } ppk ? $"PPK {ppk}"
                    : r.EkaReceiptNo is { } eka ? string.Create(CultureInfo.InvariantCulture, $"EKA-{eka}") : "—";
            sheet.Cell(row, 6).Value = r.Note ?? "";
            sheet.Cell(row, 7).Value = r.Person ?? "— nėra —";
            sheet.Cell(row, 8).Value = r.Amount;
            return;
        }
        sheet.Cell(row, 4).Value = r.Person ?? "";
        sheet.Cell(row, 5).Value = r.Note is null ? r.Customer ?? "" : $"{r.Customer} · {r.Note}";
        sheet.Cell(row, 6).Value = r.DiscountPct ?? 0;
        sheet.Cell(row, 7).Value = r.PaidEur ?? 0;
        sheet.Cell(row, 8).Value = r.Cancelled ? "Anuliuotas" : "Priimtas";
        sheet.Cell(row, 9).Value = r.Amount;
    }

    private static void Link(IXLCell cell, string url, string text) =>
        cell.FormulaA1 = $"HYPERLINK(\"{url.Replace("\"", "\"\"")}\",\"{text.Replace("\"", "\"\"")}\")";

    private static void Header(IXLWorksheet sheet, int row, IReadOnlyList<string> heads)
    {
        for (var i = 0; i < heads.Count; i++) sheet.Cell(row, i + 1).Value = heads[i];
        sheet.Range(row, 1, row, heads.Count).Style.Font.SetBold().Fill.SetBackgroundColor(HeaderFill)
            .Border.SetBottomBorder(XLBorderStyleValues.Thin);
    }

    private static string Address(int row, int column) => XLHelper.GetColumnLetterFromNumber(column) + row.ToString(CultureInfo.InvariantCulture);
}
