using System.Data;
using System.Globalization;
using System.Text;
using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Contracts.Reports;
using LKvitai.MES.Modules.Sales.Infrastructure.Sql.Security;
using Microsoft.Data.SqlClient;

namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;

/// <summary>
/// Live versions of the legacy Mango workbook reports. The SQL is the legacy one
/// (<c>sp_sumReportProduction</c>, <c>selectSalaryAccuirance</c>, <c>AtaskaitaViliusMonthPinigai</c>)
/// with the period made a parameter. Grouping and filtering only ever use the whitelisted
/// expressions below; caller text reaches SQL as parameters only.
/// </summary>
public sealed class SqlSalesReports(SalesSqlOptions options, ILegacySqlConnectionFactory connections) : ISalesReports
{
    public const int MaxRows = 2000;
    private const string Period = "period";
    private static readonly string[] Granularities = ["day", "month", "year"];

    private sealed record Definition(
        string From,
        string DateColumn,
        IReadOnlyDictionary<string, string> Dimensions,
        IReadOnlyDictionary<string, string> Measures,
        IReadOnlyList<string> RequiredGroupBy,
        IReadOnlyDictionary<string, string> DimensionValueSql);

    private static readonly Dictionary<string, Definition> Reports = new(StringComparer.OrdinalIgnoreCase)
    {
        // A product counts once per report type it maps to (e.g. "Roletai (vnt)" and "Roletai (kvm)"),
        // so totals are only meaningful per type: "type" is always part of the grouping.
        [SalesReportNames.Production] = new(
            """
            rpt.qrOrderedBlinds AS b
            INNER JOIN rpt.TCR_BlindTypes_ReportTypes AS btr ON b.iBlindTypeID = btr.iBlindTypeID
            INNER JOIN rpt.TBD_ReportsTypes AS rt ON btr.iReportTypeID = rt.iReportTypeID
            INNER JOIN rpt.TCR_OrderSeries_ReportSeries AS osr ON b.iOrderSeriesID = osr.iOrderSeriesID
            INNER JOIN rpt.TBD_ReportsSeries AS rs ON osr.iReportSeriesID = rs.iReportSeriesID
            INNER JOIN rpt.selBlindsMovementsMinDate AS mm ON b.sBlindBarCode = mm.sBarCode
            INNER JOIN dbo.Zinynas_busenos AS s ON mm.iStateID = s.BusenosID
            """,
            "mm.dtMinDate",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = "rt.sReportTypeName",
                ["unit"] = "CASE rt.iUnitID WHEN 1 THEN N'vnt' WHEN 2 THEN N'm (plotis)' WHEN 3 THEN N'm (aukštis)' WHEN 4 THEN N'm²' END",
                ["series"] = "rs.sReportSeriesName",
                ["state"] = "s.Busena",
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["quantity"] = "SUM(CASE rt.iUnitID WHEN 1 THEN b.dBlindQuantity WHEN 2 THEN b.dBlindWidth * b.dBlindQuantity WHEN 3 THEN b.dBlindHeight * b.dBlindQuantity WHEN 4 THEN b.dBlindWidth * b.dBlindHeight * b.dBlindQuantity END)",
                ["pieces"] = "SUM(b.dBlindQuantity)",
            },
            ["type", "unit"],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["type"] = "SELECT sReportTypeName FROM rpt.TBD_ReportsTypes",
                ["series"] = "SELECT sReportSeriesName FROM rpt.TBD_ReportsSeries",
                ["state"] = "SELECT Busena FROM dbo.Zinynas_busenos",
            }),

        [SalesReportNames.Work] = new(
            """
            dbo.TCR_OrdersDutiesEmployees AS x
            INNER JOIN dbo.Zinynas_darbuotojai AS e ON x.iEmployeeID = e.DarbuotojoID
            INNER JOIN dbo.TBD_Duties AS d ON x.iDutyID = d.iDutyID
            INNER JOIN dbo.Uzsakymai AS u ON x.iOrderID = u.UzsakymasID
            INNER JOIN dbo.Zinynas_UzsakymoSerijos AS sr ON u.UzsakymoSerija = sr.UzsakymoSerijaID
            INNER JOIN dbo.Zinynas_filialai AS f ON sr.FilialoID = f.FilialoID
            INNER JOIN dbo.DIC_SeriesGroupForSalaryReport AS g ON sr.SeriesGroupForSalaryReport = g.ID
            INNER JOIN dbo.Zinynas_valiutos AS v ON f.iCurrencyID = v.ValiutosID
            """,
            "x.dtAccuiranceDate",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["duty"] = "d.sName",
                ["employee"] = "e.DarbuotojoPavadinimas",
                ["department"] = "f.FilialoPavadinimas",
                ["salaryGroup"] = "g.Name",
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["amount"] = "SUM(ISNULL(x.dAccuiredAmount, 0) * v.Kursas)",
                ["orders"] = "SUM(x.dAccuiredOrderQuanity)",
                ["products"] = "SUM(x.dAccuiredBlindsQuanity)",
            },
            [],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["duty"] = "SELECT sName FROM dbo.TBD_Duties",
                ["department"] = "SELECT FilialoPavadinimas FROM dbo.Zinynas_filialai",
                ["salaryGroup"] = "SELECT Name FROM dbo.DIC_SeriesGroupForSalaryReport",
            }),

        [SalesReportNames.Payments] = new(
            """
            (SELECT Suma, MokeijimoTipasID, UzsakymasID, Data FROM dbo.Mokejimai
             UNION ALL
             SELECT Suma, MokeijimoTipasID, UzsakymasID, Data FROM MTTools.dbo.LKUZAM) AS p
            INNER JOIN dbo.Uzsakymai AS u ON p.UzsakymasID = u.UzsakymasID
            INNER JOIN dbo.Zinynas_UzsakymoSerijos AS sr ON u.UzsakymoSerija = sr.UzsakymoSerijaID
            INNER JOIN dbo.Zinynas_filialai AS f ON sr.FilialoID = f.FilialoID AND f.sCode <> N'TES'
            """,
            "p.Data",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["method"] = "CASE p.MokeijimoTipasID WHEN 1 THEN N'ppk' WHEN 834019474 THEN N'ppk' WHEN 2 THEN N'bankas' WHEN 81423987 THEN N'kasa' ELSE N'kita' END",
                ["branch"] = "f.FilialoPavadinimas",
                ["branchCode"] = "f.sCode",
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["amount"] = "SUM(p.Suma)",
                ["payments"] = "COUNT(*)",
            },
            [],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["method"] = "SELECT N'kasa' UNION ALL SELECT N'ppk' UNION ALL SELECT N'bankas' UNION ALL SELECT N'kita'",
                ["branch"] = "SELECT FilialoPavadinimas FROM dbo.Zinynas_filialai WHERE sCode <> N'TES'",
            }),
    };

    public async Task<SalesReportResult> RunAsync(SalesReportQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var def = Find(query.Report);
        if (query.To <= query.From) throw new SalesReportException("'to' must be after 'from' (it is exclusive).");
        if (query.To.DayNumber - query.From.DayNumber > 366 * 10) throw new SalesReportException("The period can be at most 10 years.");
        var granularity = (query.Granularity ?? "month").Trim().ToLowerInvariant();
        if (!Granularities.Contains(granularity)) throw new SalesReportException("granularity must be day, month or year.");

        var groupBy = ResolveGroupBy(def, query.GroupBy);
        var filters = ResolveFilters(def, query.Filters);
        var measures = def.Measures.Keys.ToList();
        var sql = BuildSql(def, groupBy, granularity, filters);

        await using var lease = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandTimeout = Math.Max(options.CommandTimeoutSeconds, 60);
        // The text is built only from the constant definitions above; every caller value is a parameter.
        cmd.CommandText = sql; // NOSONAR
        cmd.Parameters.Add(new SqlParameter("@from", SqlDbType.DateTime) { Value = query.From.ToDateTime(TimeOnly.MinValue) });
        cmd.Parameters.Add(new SqlParameter("@to", SqlDbType.DateTime) { Value = query.To.ToDateTime(TimeOnly.MinValue) });
        for (var i = 0; i < filters.Count; i++)
            cmd.Parameters.Add(new SqlParameter($"@f{i}", SqlDbType.NVarChar, 210) { Value = "%" + EscapeLike(filters[i].Value) + "%" });

        var rows = new List<SalesReportRow>();
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) rows.Add(ReadRow(reader, groupBy, measures));
        }

        var truncated = rows.Count > MaxRows;
        if (truncated) rows.RemoveAt(rows.Count - 1);
        return new SalesReportResult(query.Report.ToLowerInvariant(), query.From, query.To, groupBy, granularity, measures, rows, truncated);
    }

    private static List<string> ResolveGroupBy(Definition def, IEnumerable<string> requested)
    {
        var groupBy = new List<string>();
        foreach (var name in requested.Select(x => x.Trim()).Where(x => x.Length > 0))
        {
            var canonical = name.Equals(Period, StringComparison.OrdinalIgnoreCase) ? Period : Dimension(def, name, "dimension");
            if (!groupBy.Contains(canonical)) groupBy.Add(canonical);
        }
        groupBy.AddRange(def.RequiredGroupBy.Where(r => !groupBy.Contains(r)));
        return groupBy;
    }

    /// <summary>Filters as (whitelisted dimension, trimmed value); the value only ever becomes a parameter.</summary>
    private static List<KeyValuePair<string, string>> ResolveFilters(Definition def, IReadOnlyDictionary<string, string> filters) =>
        filters.Where(f => !string.IsNullOrWhiteSpace(f.Value))
            .Select(f => new KeyValuePair<string, string>(Dimension(def, f.Key, "filter"), f.Value.Trim()))
            .ToList();

    private static string Dimension(Definition def, string name, string what) =>
        def.Dimensions.Keys.FirstOrDefault(k => k.Equals(name, StringComparison.OrdinalIgnoreCase))
        ?? throw new SalesReportException($"Unknown {what} '{name}'. Use: {string.Join(", ", def.Dimensions.Keys)}.");

    private static string BuildSql(Definition def, IReadOnlyList<string> groupBy, string granularity, IReadOnlyList<KeyValuePair<string, string>> filters)
    {
        var periodSql = granularity switch
        {
            "day" => $"CONVERT(char(10), {def.DateColumn}, 126)",
            "year" => $"CONVERT(char(4), {def.DateColumn}, 126)",
            _ => $"CONVERT(char(7), {def.DateColumn}, 126)",
        };
        string Expr(string dim) => dim == Period ? periodSql : def.Dimensions[dim];

        // Filter i compares the i-th resolved dimension with parameter @f{i}.
        var where = new StringBuilder($"{def.DateColumn} >= @from AND {def.DateColumn} < @to");
        for (var i = 0; i < filters.Count; i++) where.Append(CultureInfo.InvariantCulture, $" AND {def.Dimensions[filters[i].Key]} LIKE @f{i}");

        var select = groupBy.Select(g => $"{Expr(g)} AS [{g}]").Concat(def.Measures.Select(m => $"{m.Value} AS [{m.Key}]"));
        var groupClause = groupBy.Count > 0 ? "GROUP BY " + string.Join(", ", groupBy.Select(Expr)) : "";
        var order = groupBy.Contains(Period) ? "[period], " : "";
        return $"SELECT TOP ({MaxRows + 1}) {string.Join(", ", select)}\nFROM {def.From}\nWHERE {where}\n{groupClause}\nORDER BY {order}[{def.Measures.Keys.First()}] DESC";
    }

    private static SalesReportRow ReadRow(SqlDataReader reader, IReadOnlyList<string> groupBy, IReadOnlyList<string> measures)
    {
        var keys = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (var k = 0; k < groupBy.Count; k++)
            keys[groupBy[k]] = reader.IsDBNull(k) ? null : Convert.ToString(reader.GetValue(k), CultureInfo.InvariantCulture)?.Trim();
        var values = new Dictionary<string, decimal>(StringComparer.Ordinal);
        for (var m = 0; m < measures.Count; m++)
        {
            var o = groupBy.Count + m;
            values[measures[m]] = reader.IsDBNull(o) ? 0m : Math.Round(Convert.ToDecimal(reader.GetValue(o), CultureInfo.InvariantCulture), 3);
        }
        return new SalesReportRow(keys, values);
    }

    public async Task<SalesReportDimensions> DimensionsAsync(string report, CancellationToken cancellationToken)
    {
        var def = Find(report);
        var values = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        await using var lease = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        foreach (var (dim, sql) in def.DimensionValueSql)
        {
            await using var cmd = lease.Connection.CreateCommand();
            cmd.CommandTimeout = options.CommandTimeoutSeconds;
            // A constant reference query from the definitions above; no caller input.
            cmd.CommandText = $"SELECT DISTINCT LTRIM(RTRIM(v)) FROM ({sql}) AS t(v) WHERE v IS NOT NULL AND LTRIM(RTRIM(v)) <> N'' ORDER BY 1"; // NOSONAR
            var list = new List<string>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) list.Add(reader.GetString(0));
            values[dim] = list;
        }
        return new SalesReportDimensions(report.ToLowerInvariant(), [Period, .. def.Dimensions.Keys], [.. def.Measures.Keys], values);
    }

    private static Definition Find(string report) =>
        Reports.TryGetValue(report ?? "", out var def)
            ? def
            : throw new SalesReportException($"Unknown report '{report}'. Use: {string.Join(", ", Reports.Keys)}.");

    private static string EscapeLike(string value) =>
        value.Length > 100 ? throw new SalesReportException("A filter can be at most 100 characters.")
            : value.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
}
