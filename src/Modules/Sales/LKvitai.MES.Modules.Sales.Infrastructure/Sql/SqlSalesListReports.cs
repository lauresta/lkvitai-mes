using System.Data;
using System.Globalization;
using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Contracts.Reports;
using LKvitai.MES.Modules.Sales.Infrastructure.Sql.Security;
using Microsoft.Data.SqlClient;

namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;

/// <summary>
/// Operational lists over LKvitaiDb (current production queue, material popularity, stopped products,
/// unpaid orders, made but not handed out / installed, customer debt, order search). Each list is one
/// constant embedded query (<c>Sql/Queries/Report*.sql</c>); caller values only ever arrive as typed parameters.
/// </summary>
public sealed class SqlSalesListReports(SalesSqlOptions options, ILegacySqlConnectionFactory connections) : ISalesListReports
{
    public const int MaxRows = 1000;

    private sealed record Param(string Name, SqlDbType Type, object? Default, int Max = int.MaxValue);
    private sealed record ListDefinition(string Sql, IReadOnlyList<Param> Params);

    private static readonly Dictionary<string, ListDefinition> Lists = new(StringComparer.OrdinalIgnoreCase)
    {
        [SalesListReportNames.Pipeline] = new(Read("ReportPipeline.sql"),
            [new("branch", SqlDbType.NVarChar, null), new("maxOrderAgeDays", SqlDbType.Int, null), new("staleDays", SqlDbType.Int, 30)]),
        [SalesListReportNames.Materials] = new(Read("ReportMaterials.sql"),
            [new("from", SqlDbType.DateTime, null), new("to", SqlDbType.DateTime, null), new("typeSearch", SqlDbType.NVarChar, null),
             new("materialSearch", SqlDbType.NVarChar, null), new("top", SqlDbType.Int, 50, 500)]),
        [SalesListReportNames.Stopped] = new(Read("ReportStopped.sql"),
            [new("search", SqlDbType.NVarChar, null), new("includeCancelled", SqlDbType.Bit, false)]),
        [SalesListReportNames.UnpaidOrders] = new(Read("ReportUnpaidOrders.sql"),
            [new("minDays", SqlDbType.Int, 30), new("minDebt", SqlDbType.Money, 0.01m), new("onlyHandedOut", SqlDbType.Bit, false),
             new("onlyLegacyOverdue", SqlDbType.Bit, false), new("search", SqlDbType.NVarChar, null)]),
        [SalesListReportNames.NotDelivered] = new(Read("ReportNotDelivered.sql"),
            [new("days", SqlDbType.Int, 20), new("allMade", SqlDbType.Bit, true), new("includeInstall", SqlDbType.Bit, true),
             new("maxOrderAgeDays", SqlDbType.Int, 365)]),
        [SalesListReportNames.CustomerDebt] = new(Read("ReportCustomerDebt.sql"),
            [new("search", SqlDbType.NVarChar, null), new("onlyHandedOut", SqlDbType.Bit, false), new("top", SqlDbType.Int, 20, 500)]),
        [SalesListReportNames.OrderSearch] = new(Read("ReportOrderSearch.sql"),
            [new("from", SqlDbType.DateTime, null), new("to", SqlDbType.DateTime, null), new("typeSearch", SqlDbType.NVarChar, null),
             new("materialSearch", SqlDbType.NVarChar, null), new("state", SqlDbType.NVarChar, null), new("branch", SqlDbType.NVarChar, null),
             new("includeCancelled", SqlDbType.Bit, false), new("top", SqlDbType.Int, 200, 1000)]),
    };

    public async Task<SalesListResult> RunAsync(string name, IReadOnlyDictionary<string, string> args, CancellationToken cancellationToken)
    {
        if (!Lists.TryGetValue(name ?? "", out var def))
            throw new SalesReportException($"Unknown list '{name}'. Use: {string.Join(", ", Lists.Keys)}.");
        var unknown = args.Keys.FirstOrDefault(k => !def.Params.Any(p => p.Name.Equals(k, StringComparison.OrdinalIgnoreCase)));
        if (unknown is not null)
            throw new SalesReportException($"Unknown parameter '{unknown}'. Use: {string.Join(", ", def.Params.Select(p => p.Name))}.");

        var parameters = def.Params.Select(p => Bind(p, args)).ToList();
        await using var lease = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = lease.Connection.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandTimeout = Math.Max(options.CommandTimeoutSeconds, 60);
        cmd.CommandText = def.Sql;
        cmd.Parameters.AddRange(parameters.ToArray());

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        var columns = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        for (var i = 0; i < reader.FieldCount; i++) columns.Add(reader.GetName(i));
        var truncated = false;
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (rows.Count == MaxRows) { truncated = true; break; }
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++) row[columns[i]] = Value(reader, i);
            rows.Add(row);
        }
        return new SalesListResult(name!.ToLowerInvariant(), columns, rows, truncated);
    }

    private static SqlParameter Bind(Param p, IReadOnlyDictionary<string, string> args)
    {
        var raw = args.FirstOrDefault(a => a.Key.Equals(p.Name, StringComparison.OrdinalIgnoreCase)).Value?.Trim();
        object? value = string.IsNullOrEmpty(raw) ? p.Default : Parse(p, raw);
        if (value is null && p.Type == SqlDbType.DateTime)
        {
            // Periods default to the current month: from its first day up to and including today ("to" is exclusive).
            var today = DateTime.Today;
            value = p.Name == "from" ? new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Unspecified) : today.AddDays(1);
        }
        var parameter = new SqlParameter("@" + p.Name, p.Type) { Value = value ?? DBNull.Value };
        if (p.Type == SqlDbType.NVarChar) parameter.Size = 100;
        return parameter;
    }

    private static object Parse(Param p, string raw)
    {
        try
        {
            return p.Type switch
            {
                SqlDbType.Int => Math.Clamp(int.Parse(raw, CultureInfo.InvariantCulture), 0, p.Max),
                SqlDbType.Money => decimal.Parse(raw, CultureInfo.InvariantCulture),
                SqlDbType.Bit => bool.Parse(raw),
                SqlDbType.DateTime => DateOnly.ParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToDateTime(TimeOnly.MinValue),
                _ => raw.Length <= 100 ? raw : throw new SalesReportException($"'{p.Name}' can be at most 100 characters."),
            };
        }
        catch (FormatException)
        {
            throw new SalesReportException($"'{p.Name}' has an invalid value.");
        }
    }

    private static object? Value(SqlDataReader reader, int i)
    {
        if (reader.IsDBNull(i)) return null;
        return reader.GetValue(i) switch
        {
            DateTime d => d.TimeOfDay == TimeSpan.Zero ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : d.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            decimal m => Math.Round(m, 2),
            string s => s.Trim(),
            var other => other,
        };
    }

    private static string Read(string file)
    {
        using var stream = typeof(SqlSalesListReports).Assembly.GetManifestResourceStream($"LKvitai.MES.Modules.Sales.Infrastructure.Sql.Queries.{file}")
            ?? throw new InvalidOperationException($"Missing Sales report query {file}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
