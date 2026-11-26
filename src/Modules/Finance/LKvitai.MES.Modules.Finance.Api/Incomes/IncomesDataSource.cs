using System.Data;
using LKvitai.MES.Modules.Finance.Api.BankPayments;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

namespace LKvitai.MES.Modules.Finance.Api.Incomes;

/// <summary>
/// Read-only access to the legacy database for the incomes report. Every query is parameterised and runs under the
/// user's own legacy SQL login (EXECUTE AS LOGIN). Rows of a period are cached for a minute per login, so the summary,
/// the table pages and the export of one screen read the database once.
/// </summary>
internal sealed class IncomesDataSource(FinanceAccess access, IMemoryCache cache, FinanceActor actor) : IAsyncDisposable
{
    private static readonly TimeSpan RowsLifetime = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan BranchesLifetime = TimeSpan.FromMinutes(5);
    private FinanceSqlLease? _lease;

    /// <summary>Test branch excluded the same way as the Sales reports (sCode TES).</summary>
    internal const string NotTestBranch = "ISNULL(f.sCode, N'') <> N'TES'";

    internal const string BranchesSql = $"""
        SELECT f.FilialoID, ISNULL(NULLIF(LTRIM(RTRIM(f.sCode)), N''), N'F' + CAST(f.FilialoID AS nvarchar(10))),
               ISNULL(f.FilialoPavadinimas, N'')
        FROM dbo.Zinynas_filialai f
        WHERE {NotTestBranch}
        ORDER BY f.FilialoID
        """;

    /// <summary>
    /// Money received: MTTools.dbo.LKUZAM (employee required) and dbo.Mokejimai (employee optional), as the legacy
    /// window (sproc_Monkey, @Mark = 'c') combined them, by payment date inclusive of both ends.
    /// </summary>
    internal const string PaymentsSql = $"""
        SELECT p.Src, p.MokejimoID, p.Data, p.dtDate, ISNULL(p.Suma, 0) * ISNULL(v.Kursas, 1), p.MokeijimoTipasID,
               us.FilialoID, ISNULL(us.UzsakymoSerijaPav, N''),
               ISNULL(us.UzsakymoSerijaPav, N'') + N'-' + CAST(u.UzsakymoNumeris AS nvarchar(20)) + N'-' + ISNULL(u.UzsakymoGrupe, N''),
               p.Employee, p.Pastabos, p.PPKKvitasNR, p.BankTransferNR, p.EKA_ID
        FROM (
            SELECT N'L' AS Src, l.MokejimoID, l.UzsakymasID, l.Data, l.dtDate, l.Suma, l.MokeijimoTipasID, l.Pastabos,
                   l.PPKKvitasNR, l.BankTransferNR, l.EKA_ID, d.DarbuotojoPavadinimas AS Employee
            FROM MTTools.dbo.LKUZAM l
            INNER JOIN dbo.Zinynas_darbuotojai d ON l.EmployeeID = d.DarbuotojoID
            WHERE l.Data >= @from AND l.Data < @to
            UNION ALL
            SELECT N'M', m.MokejimoID, m.UzsakymasID, m.Data, m.dtDate, m.Suma, m.MokeijimoTipasID, m.Pastabos,
                   m.PPKKvitasNR, m.BankTransferNR, m.EKA_ID, d.DarbuotojoPavadinimas
            FROM dbo.Mokejimai m
            LEFT JOIN dbo.Zinynas_darbuotojai d ON m.EmployeeID = d.DarbuotojoID
            WHERE m.Data >= @from AND m.Data < @to
        ) p
        INNER JOIN dbo.Uzsakymai u ON p.UzsakymasID = u.UzsakymasID
        INNER JOIN dbo.Zinynas_UzsakymoSerijos us ON u.UzsakymoSerija = us.UzsakymoSerijaID
        INNER JOIN dbo.Zinynas_filialai f ON us.FilialoID = f.FilialoID
        INNER JOIN dbo.Zinynas_valiutos v ON f.iCurrencyID = v.ValiutosID
        INNER JOIN dbo.Zinynas_mokejimotipai mt ON p.MokeijimoTipasID = mt.MokejimoTipasID
        WHERE {NotTestBranch} AND p.MokeijimoTipasID IN ({IncomesMethods.LegacyIds})
        """;

    /// <summary>
    /// Orders taken (sproc_Monkey, @Mark = 'o'): not spoiled, or cancelled at the client's request (valued at what the
    /// client still pays, MTTools.dbo.LKUZA.Verte), by order date inclusive of both ends. Paid = all payments of the order.
    /// </summary>
    internal const string OrdersSql = $"""
        SELECT u.UzsakymasID, u.UzsakymoData, u.dtDateInsert, us.FilialoID, ISNULL(us.UzsakymoSerijaPav, N''),
               ISNULL(us.UzsakymoSerijaPav, N'') + N'-' + CAST(u.UzsakymoNumeris AS nvarchar(20)) + N'-' + ISNULL(u.UzsakymoGrupe, N''),
               CAST(CASE WHEN u.sNotes = N'{CancelledNote}' THEN 1 ELSE 0 END AS bit),
               ISNULL(CASE WHEN u.sNotes = N'{CancelledNote}' THEN CAST(z.Verte AS decimal(19, 4))
                           ELSE u.UzsakymoVerteSuNuolaida END, 0) * ISNULL(v.Kursas, 1),
               ISNULL(u.Nuolaida, 0), k.DarbuotojoPavadinimas, c.UzsakovoPavadinimas, u.sNotes,
               ISNULL(paid.Amount, 0) * ISNULL(v.Kursas, 1)
        FROM dbo.Uzsakymai u
        INNER JOIN dbo.Zinynas_UzsakymoSerijos us ON u.UzsakymoSerija = us.UzsakymoSerijaID
        INNER JOIN dbo.Zinynas_filialai f ON us.FilialoID = f.FilialoID
        INNER JOIN dbo.Zinynas_valiutos v ON f.iCurrencyID = v.ValiutosID
        LEFT JOIN MTTools.dbo.LKUZA z ON z.iOrderID = u.UzsakymasID
        LEFT JOIN dbo.Zinynas_darbuotojai k ON k.DarbuotojoID = u.KonsultantoID
        LEFT JOIN dbo.Zinynas_uzsakovai c ON c.UzsakovoID = u.UzsakovoID
        OUTER APPLY (
            SELECT SUM(x.Suma) AS Amount FROM (
                SELECT m.Suma FROM dbo.Mokejimai m WHERE m.UzsakymasID = u.UzsakymasID
                UNION ALL
                SELECT ISNULL(l.Suma, 0) FROM MTTools.dbo.LKUZAM l WHERE l.UzsakymasID = u.UzsakymasID) x
        ) paid
        WHERE (u.Sugadintas = 0 OR u.sNotes = N'{CancelledNote}')
          AND u.UzsakymoData >= @from AND u.UzsakymoData < @to AND {NotTestBranch}
        """;

    /// <summary>The legacy marker of an order cancelled at the client's request.</summary>
    internal const string CancelledNote = "Kliento prasymu anuliuotas";

    public Task<IReadOnlyList<IncomesBranch>> BranchesAsync(CancellationToken ct) =>
        Cached($"incomes:branches:{actor.LegacyLogin}", BranchesLifetime, () => Read(BranchesSql, null,
            reader => new IncomesBranch(reader.GetInt32(0), reader.GetString(1).Trim(), reader.GetString(2).Trim()), ct));

    public Task<IReadOnlyList<IncomesRecord>> RecordsAsync(IncomesDataset dataset, DateOnly from, DateOnly to,
        CancellationToken ct) =>
        Cached($"incomes:{dataset}:{from:yyyyMMdd}:{to:yyyyMMdd}:{actor.LegacyLogin}", RowsLifetime,
            () => dataset == IncomesDataset.Money
                ? Read(PaymentsSql, IncomesPeriod.SqlBounds(from, to), Payment, ct)
                : Read(OrdersSql, IncomesPeriod.SqlBounds(from, to), Order, ct));

    private async Task<IReadOnlyList<T>> Cached<T>(string key, TimeSpan lifetime, Func<Task<IReadOnlyList<T>>> load)
    {
        if (cache.TryGetValue(key, out IReadOnlyList<T>? cached) && cached is not null) return cached;
        var value = await load();
        cache.Set(key, value, lifetime);
        return value;
    }

    private async Task<IReadOnlyList<T>> Read<T>(string sql, (DateTime From, DateTime To)? bounds,
        Func<SqlDataReader, T?> map, CancellationToken ct) where T : class
    {
        _lease ??= await access.OpenAsync(actor, ct, "Cikada Finance incomes");
        await using var command = new SqlCommand(sql, _lease.Connection) { CommandTimeout = 60 };
        if (bounds is { } b)
        {
            command.Parameters.Add("@from", SqlDbType.DateTime).Value = b.From;
            command.Parameters.Add("@to", SqlDbType.DateTime).Value = b.To;
        }
        var result = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            if (map(reader) is { } item) result.Add(item);
        return result;
    }

    private static IncomesRecord? Payment(SqlDataReader r)
    {
        var method = IncomesMethods.FromLegacyId(r.GetInt32(5));
        if (method is null) return null;
        string? Text(int i) => r.IsDBNull(i) ? null : r.GetString(i).Trim() is { Length: > 0 } value ? value : null;
        int? Positive(int i) => r.IsDBNull(i) || r.GetInt32(i) <= 0 ? null : r.GetInt32(i);
        return new IncomesRecord(
            Id: $"{r.GetString(0)}{r.GetInt64(1)}",
            Date: DateOnly.FromDateTime(r.GetDateTime(2)),
            EnteredAt: r.IsDBNull(3) ? null : r.GetDateTime(3),
            OrderNo: r.GetString(8),
            BranchId: r.GetInt32(6),
            Series: r.GetString(7),
            Method: method,
            Amount: Math.Round(r.GetDecimal(4), 2),
            Person: Text(9),
            Note: Text(10),
            PpkNo: method == IncomesMethods.Ppk ? Text(11) : null,
            BankTransferNo: method == IncomesMethods.Bank ? Positive(12) : null,
            EkaReceiptNo: method == IncomesMethods.Eka ? Positive(13) : null);
    }

    private static IncomesRecord Order(SqlDataReader r)
    {
        string? Text(int i) => r.IsDBNull(i) ? null : r.GetString(i).Trim() is { Length: > 0 } value ? value : null;
        var cancelled = r.GetBoolean(6);
        return new IncomesRecord(
            Id: r.GetInt64(0).ToString(System.Globalization.CultureInfo.InvariantCulture),
            Date: DateOnly.FromDateTime(r.GetDateTime(1)),
            EnteredAt: r.IsDBNull(2) ? null : r.GetDateTime(2),
            OrderNo: r.GetString(5),
            BranchId: r.GetInt32(3),
            Series: r.GetString(4),
            Method: null,
            Amount: Math.Round(r.GetDecimal(7), 2),
            Person: Text(9),
            Note: cancelled ? null : Text(11),
            Customer: Text(10),
            DiscountPct: Math.Round(r.GetDecimal(8), 1),
            PaidEur: Math.Round(r.GetDecimal(12), 2),
            Cancelled: cancelled);
    }

    public async ValueTask DisposeAsync()
    {
        if (_lease is not null) await _lease.DisposeAsync();
    }
}
