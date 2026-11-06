using System.Data;
using Microsoft.Data.SqlClient;

namespace LKvitai.MES.Modules.Finance.Api.BankPayments;

internal sealed class BankRepository(FinanceAccess access, BankImportSessions sessions, IConfiguration config)
{
    private static readonly Dictionary<string, string> SortColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["nr"] = "NR", ["date"] = "Data", ["firm"] = "Firma", ["purpose"] = "Paskirtis", ["sum"] = "Suma"
    };
    private static string Country(string value) => value is "LT" or "LV" ? value : throw new BankOperationException(400, "Invalid country.");
    private static SqlCommand Command(SqlConnection c, string sql, SqlTransaction? tx = null) => new(sql, c, tx) { CommandTimeout = 20 };
    private static void Add(SqlCommand c, string key, object? value) => c.Parameters.AddWithValue(key, value ?? DBNull.Value);
    private static BankPaymentRow Row(SqlDataReader r) => new(r.GetInt32(0), DateOnly.FromDateTime(r.GetDateTime(1)),
        r.GetString(2).TrimEnd(), r.GetString(3).TrimEnd(), r.GetDecimal(4));

    public async Task<BankPaymentPage> List(FinanceActor actor, string country, DateOnly? from, string? q,
        string? sort, bool desc, int page, CancellationToken ct)
    {
        country = Country(country);
        page = Math.Max(1, page);
        if (page > 10000) throw new BankOperationException(400, "Invalid page.");
        var col = SortColumns.TryGetValue(sort ?? "nr", out var selected) ? selected : "NR";
        var direction = desc ? "DESC" : "ASC";
        var order = col == "NR" ? $"NR {direction}" : $"{col} {direction}, NR DESC";
        var search = (q ?? "").Trim();
        if (search.Length > 100) throw new BankOperationException(400, "Search is too long.");
        await using var lease = await access.OpenAsync(actor, ct);
        var c = lease.Connection;
        using var cmd = Command(c, $"""
            SELECT COUNT(*), MAX(Data) FROM dbo.BankTransfersTable WHERE Country=@country;
            SELECT COUNT(*) FROM dbo.BankTransfersTable WHERE Country=@country
              AND (@from IS NULL OR Data>=@from)
              AND (@q='' OR Firma LIKE @pattern OR Paskirtis LIKE @pattern);
            SELECT NR,Data,Firma,Paskirtis,Suma FROM dbo.BankTransfersTable
            WHERE Country=@country AND (@from IS NULL OR Data>=@from)
              AND (@q='' OR Firma LIKE @pattern OR Paskirtis LIKE @pattern)
            ORDER BY {order} OFFSET @offset ROWS FETCH NEXT 100 ROWS ONLY;
            """);
        Add(cmd, "@country", country);
        Add(cmd, "@from", from?.ToDateTime(TimeOnly.MinValue));
        Add(cmd, "@q", search);
        Add(cmd, "@pattern", "%" + search.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%");
        Add(cmd, "@offset", (page - 1) * 100);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        var countryTotal = r.GetInt32(0);
        var last = r.IsDBNull(1) ? (DateOnly?)null : DateOnly.FromDateTime(r.GetDateTime(1));
        await r.NextResultAsync(ct); await r.ReadAsync(ct);
        var total = r.GetInt32(0);
        await r.NextResultAsync(ct);
        var rows = new List<BankPaymentRow>();
        while (await r.ReadAsync(ct)) rows.Add(Row(r));
        return new BankPaymentPage(rows, total, countryTotal, last, page, 100);
    }

    public async Task<BankPasteResult> Parse(FinanceActor actor, BankPasteRequest request, IConfiguration config, CancellationToken ct)
    {
        var country = Country(request.Country);
        var parsed = BankPasteParser.Parse(request.Text).ToArray();
        if (parsed.Length == 0) return new BankPasteResult(parsed);
        var dates = parsed.Where(x => x.Date.HasValue).Select(x => x.Date!.Value).ToArray();
        if (dates.Length == 0) return new BankPasteResult(parsed);
        var mode = config["Finance:DuplicateKey"] == "DateSum" ? "DateSum" : "DateFirmSum";
        static string Key(DateOnly date, string firm, decimal sum, string mode) =>
            $"{date:yyyyMMdd}|{(mode == "DateFirmSum" ? firm.Trim().ToUpperInvariant() : "")}|{sum:0.0000}";
        var existing = new HashSet<string>(StringComparer.Ordinal);
        await using (var lease = await access.OpenAsync(actor, ct))
        {
            using var cmd = Command(lease.Connection, """
                SELECT Data,Firma,Suma FROM dbo.BankTransfersTable
                WHERE Country=@country AND Data>=@from AND Data<@to
                """);
            Add(cmd, "@country", country);
            Add(cmd, "@from", dates.Min().ToDateTime(TimeOnly.MinValue));
            Add(cmd, "@to", dates.Max().AddDays(1).ToDateTime(TimeOnly.MinValue));
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) existing.Add(Key(DateOnly.FromDateTime(reader.GetDateTime(0)),
                reader.GetString(1), reader.GetDecimal(2), mode));
        }
        var batch = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < parsed.Length; i++)
        {
            var row = parsed[i];
            if (row.Status == "Error" || row.Date is null || row.Sum is null) continue;
            var key = Key(row.Date.Value, row.Firm, row.Sum.Value, mode);
            if (existing.Contains(key) || !batch.Add(key)) parsed[i] = row with { Status = "Duplicate", Selected = false };
        }
        return new BankPasteResult(parsed);
    }

    public async Task<BankImportResult> Import(FinanceActor actor, BankImportRequest request, CancellationToken ct)
    {
        var country = Country(request.Country);
        if (request.Rows is not { Count: > 0 and <= 1500 }) throw new BankOperationException(400, "Select 1–1500 rows.");
        foreach (var row in request.Rows)
            if (row is null || row.Date.Year is < 2000 or > 2100 || string.IsNullOrWhiteSpace(row.Firm) ||
                row.Firm.Length > 255 || row.Purpose is null || row.Purpose.Length > 1024 ||
                row.Sum == 0 || Math.Abs(row.Sum) > 999_999_999m || decimal.Round(row.Sum, 4) != row.Sum)
                throw new BankOperationException(400, "Invalid transfer row.");
        await using var lease = await access.OpenAsync(actor, ct);
        var c = lease.Connection;
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            using var next = Command(c, "SELECT ISNULL(MAX(NR),0) FROM dbo.BankTransfersTable WITH (TABLOCKX, HOLDLOCK)", tx);
            var max = Convert.ToInt32(await next.ExecuteScalarAsync(ct));
            if (max > int.MaxValue - request.Rows.Count) throw new BankOperationException(409, "NR range exhausted.");
            var saved = new List<BankPaymentRow>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var byDateSum = config["Finance:DuplicateKey"] == "DateSum";
            foreach (var row in request.Rows)
            {
                var duplicateKey = $"{row.Date:yyyyMMdd}|{(byDateSum ? "" : row.Firm.Trim().ToUpperInvariant())}|{row.Sum:0.0000}";
                if (!seen.Add(duplicateKey) && !row.AllowDuplicate)
                    throw new BankOperationException(409, "Duplicate row in this import. Review the preview again.");
                using var duplicate = Command(c, $"""
                    SELECT COUNT(*) FROM dbo.BankTransfersTable WHERE Country=@country AND Data>=@date
                    AND Data<DATEADD(day,1,@date) AND Suma=@sum
                    {(byDateSum ? "" : "AND UPPER(LTRIM(RTRIM(Firma)))=UPPER(@firm)")}
                    """, tx);
                Add(duplicate, "@country", country); Add(duplicate, "@date", row.Date.ToDateTime(TimeOnly.MinValue));
                Add(duplicate, "@sum", row.Sum); if (!byDateSum) Add(duplicate, "@firm", row.Firm.Trim());
                if (Convert.ToInt32(await duplicate.ExecuteScalarAsync(ct)) > 0 && !row.AllowDuplicate)
                    throw new BankOperationException(409, "Transfer already exists. Review the preview again.");
                var nr = ++max;
                using var insert = Command(c, """
                    INSERT dbo.BankTransfersTable(NR,Data,Firma,Paskirtis,Suma,Country)
                    VALUES(@nr,@date,@firm,@purpose,@sum,@country)
                    """, tx);
                Add(insert, "@nr", nr); Add(insert, "@date", row.Date.ToDateTime(TimeOnly.MinValue));
                Add(insert, "@firm", row.Firm.Trim()); Add(insert, "@purpose", row.Purpose.Trim());
                Add(insert, "@sum", row.Sum); Add(insert, "@country", country);
                await insert.ExecuteNonQueryAsync(ct);
                saved.Add(new BankPaymentRow(nr, row.Date, row.Firm.Trim(), row.Purpose.Trim(), row.Sum));
            }
            await tx.CommitAsync(ct);
            var id = Guid.NewGuid();
            sessions.Save(new BankImportBatch(id, actor.UserId, saved, DateTimeOffset.UtcNow));
            return new BankImportResult(id, saved[0].Nr, saved[^1].Nr, saved.Count);
        }
        catch { await tx.RollbackAsync(CancellationToken.None); throw; }
    }

    public async Task Delete(FinanceActor actor, int nr, CancellationToken ct)
    {
        if (nr <= 0) throw new BankOperationException(400, "Invalid NR.");
        await using var lease = await access.OpenAsync(actor, ct);
        using var cmd = Command(lease.Connection, """
            DELETE b FROM dbo.BankTransfersTable b
            WHERE b.NR=@nr AND ISNULL(b.mAssignedSum,0)=0
              AND NOT EXISTS(SELECT 1 FROM dbo.Mokejimai m WHERE m.BankTransferNR=b.NR)
            """);
        Add(cmd, "@nr", nr);
        if (await cmd.ExecuteNonQueryAsync(ct) != 1)
            throw new BankOperationException(409, "Transfer is missing or already assigned to an order.");
    }

    public async Task Undo(FinanceActor actor, Guid importId, CancellationToken ct)
    {
        var batch = sessions.Get(actor.UserId, importId) ?? throw new BankOperationException(409, "This import can no longer be undone.");
        await using var lease = await access.OpenAsync(actor, ct);
        var c = lease.Connection;
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            foreach (var row in batch.Rows)
            {
                using var cmd = Command(c, """
                    DELETE b FROM dbo.BankTransfersTable b WHERE b.NR=@nr AND b.Data=@date AND b.Firma=@firm
                    AND b.Paskirtis=@purpose AND b.Suma=@sum AND ISNULL(b.mAssignedSum,0)=0
                    AND NOT EXISTS(SELECT 1 FROM dbo.Mokejimai m WHERE m.BankTransferNR=b.NR)
                    """, tx);
                Add(cmd, "@nr", row.Nr); Add(cmd, "@date", row.Date.ToDateTime(TimeOnly.MinValue));
                Add(cmd, "@firm", row.Firm); Add(cmd, "@purpose", row.Purpose); Add(cmd, "@sum", row.Sum);
                if (await cmd.ExecuteNonQueryAsync(ct) != 1)
                    throw new BankOperationException(409, "An imported transfer changed or was assigned; undo was cancelled.");
            }
            await tx.CommitAsync(ct);
            sessions.Remove(actor.UserId, importId);
        }
        catch { await tx.RollbackAsync(CancellationToken.None); throw; }
    }
}
