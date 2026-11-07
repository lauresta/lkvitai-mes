using System.Data;
using System.Security.Claims;
using LKvitai.MES.Modules.Shopfloor.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LKvitai.MES.Modules.Shopfloor.Api.Endpoints;

public sealed record RoletaiPaperlessRow(string Uzsakymas, DateTime UzsakymoData,
    long ParduotoGaminioID, string TipoTrPavadinimas, string? Valdymas, long Rusis,
    decimal? Plotis, decimal? Aukstis, decimal Kiekis, string? Spalva,
    string? Audinys, string? Kasete, string? Velenas, string? Atsvaras,
    string? Kreipianciosios, string? KrepianciujuTipas, string? Dekoras,
    string? AudinioSunaudojimas, string? Pastabos, string? Cechas,
    string? GamibinesUzduotiesNumeris, bool InProduction);
public sealed record RoletaiPaperlessMark(long ProductId, int Step, bool Marked, DateTimeOffset UpdatedAt);
public sealed record RoletaiPaperlessSignOff(long ProductId, Guid UserId, string Name,
    DateTimeOffset SignedAt, bool Mine);
public sealed record RoletaiPaperlessData(string OrderStatus, DateOnly? From, DateOnly? To,
    IReadOnlyList<RoletaiPaperlessRow> Rows,
    IReadOnlyList<ProductionReportAccessory> Accessories,
    IReadOnlyList<RoletaiPaperlessMark> MyMarks,
    IReadOnlyList<RoletaiPaperlessSignOff> SignOffs);
public sealed record RoletaiMarkRequest(bool Marked);

public static class RoletaiPaperlessEndpoints
{
    public static RouteGroupBuilder MapRoletaiPaperlessEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/roletai", async (string? order, DateOnly? from, DateOnly? to,
            RoletaiPaperlessReader reader, RoletaiPaperlessStore store,
            ProductionQueueReader queue, CancellationToken ct) =>
        {
            if (order?.Length > 80 || from > to)
                return Results.BadRequest(new { code = "INVALID_ROLETai_FILTER" });
            try
            {
                var actor = await queue.ResolveActorAsync(ct);
                var data = await reader.ReadAsync(order, from, to, ct);
                var ids = data.Rows.Select(x => x.ParduotoGaminioID).ToHashSet();
                var (marks, signOffs) = await store.ReadAsync(ids, actor, ct);
                return Results.Ok(data with { MyMarks = marks, SignOffs = signOffs });
            }
            catch (ProductionQueueAccessException ex) { return ProductionReadErrors.Access(ex); }
            catch (SqlException ex) when (ex.Number is 229 or 230) { return ProductionReadErrors.PermissionDenied(); }
        });
        group.MapPut("/roletai/items/{productId:long}/marks/{step:int}",
            async (long productId, int step, RoletaiMarkRequest request,
                RoletaiPaperlessReader reader, RoletaiPaperlessStore store,
                ProductionQueueReader queue, CancellationToken ct) =>
            {
                if (productId <= 0 || step is < 1 or > 3)
                    return Results.BadRequest(new { code = "INVALID_ROLETai_MARK" });
                try
                {
                    var actor = await queue.ResolveActorAsync(ct);
                    if (!await reader.CanMarkAsync(productId, ct)) return Results.NotFound();
                    return Results.Ok(await store.SetMarkAsync(productId, actor, step, request.Marked, ct));
                }
                catch (ProductionQueueAccessException ex) { return ProductionReadErrors.Access(ex); }
                catch (SqlException ex) when (ex.Number is 229 or 230) { return ProductionReadErrors.PermissionDenied(); }
            });
        group.MapPost("/roletai/items/{productId:long}/signoff",
            async (long productId, RoletaiPaperlessReader reader, RoletaiPaperlessStore store,
                ProductionQueueReader queue, HttpContext http, CancellationToken ct) =>
            {
                if (productId <= 0) return Results.BadRequest(new { code = "INVALID_ROLETai_ITEM" });
                try
                {
                    var actor = await queue.ResolveActorAsync(ct);
                    if (!await reader.CanMarkAsync(productId, ct)) return Results.NotFound();
                    var name = http.User.FindFirstValue(ClaimTypes.Name) ?? http.User.Identity?.Name ?? actor.ToString();
                    return Results.Ok(await store.SignAsync(productId, actor, name[..Math.Min(name.Length, 160)], ct));
                }
                catch (ProductionQueueAccessException ex) { return ProductionReadErrors.Access(ex); }
                catch (SqlException ex) when (ex.Number is 229 or 230) { return ProductionReadErrors.PermissionDenied(); }
            });
        group.MapDelete("/roletai/items/{productId:long}/signoff",
            async (long productId, RoletaiPaperlessReader reader, RoletaiPaperlessStore store,
                ProductionQueueReader queue, CancellationToken ct) =>
            {
                if (productId <= 0) return Results.BadRequest(new { code = "INVALID_ROLETai_ITEM" });
                try
                {
                    var actor = await queue.ResolveActorAsync(ct);
                    if (!await reader.CanMarkAsync(productId, ct)) return Results.NotFound();
                    await store.WithdrawAsync(productId, actor, ct);
                    return Results.NoContent();
                }
                catch (ProductionQueueAccessException ex) { return ProductionReadErrors.Access(ex); }
                catch (SqlException ex) when (ex.Number is 229 or 230) { return ProductionReadErrors.PermissionDenied(); }
            });
        return group;
    }
}

public sealed class RoletaiPaperlessReader(ProductionQueueReader queue)
{
    internal const string Categories = "-270492797,1170057273,1172751776,1172751777,1172751778,1172751779,189060769,1172751775,1172751784";
    private const string OrderNumber = "us.UzsakymoSerijaPav+N'-'+CONVERT(nvarchar(15),u.UzsakymoNumeris)+N'-'+u.UzsakymoGrupe";
    private static readonly string BaseFrom = $"""
        FROM dbo.Uzsakymai u
        JOIN dbo.Zinynas_UzsakymoSerijos us ON us.UzsakymoSerijaID=u.UzsakymoSerija
        JOIN dbo.ParduotiGaminiai pg ON pg.UzsakymoID=u.UzsakymasID
        JOIN dbo.Zinynas_tipai t ON t.TipoID=pg.ParduotoGaminioTipoID
        LEFT JOIN dbo.TBD_Components c ON c.iComponentID=pg.Spalva
        OUTER APPLY (
            SELECT TOP (1) d.sProductionOutputDocumentNo
            FROM dbo.TAB_ProductionOutputDocumentDetails dd
            JOIN dbo.TAB_ProductionOutputDocuments d
              ON d.iProductionOutputDocumentID=dd.iProductionOutputDocumentID
            WHERE dd.iBarCode=pg.iBarCode AND d.sProductionOutputDocumentNo LIKE N'GU%'
            ORDER BY CASE WHEN d.iProductionOutputDocumentID=pg.GamibinesUzduotiesID THEN 0 ELSE 1 END,
                     d.dtDateTime DESC,d.iProductionOutputDocumentID DESC
        ) task
        WHERE EXISTS(SELECT 1 FROM dbo.Loc_leidimas() acl WHERE acl.UKS=us.FilialoID)
          AND ISNULL(u.Sugadintas,0)=0
          AND ISNULL(u.sNotes,N'')<>N'Kliento prasymu anuliuotas'
          AND t.Rusis IN ({Categories})
          AND (pg.iCountOfRepairs IS NULL OR pg.iCountOfRepairs=0)
        AND (@order IS NULL OR CHARINDEX(@order,UPPER(REPLACE({OrderNumber},N' ',N'')))>0)
          AND (@order IS NOT NULL OR (pg.BusenosID=1
              AND (@from IS NULL OR u.UzsakymoData>=@from)
              AND (@to IS NULL OR u.UzsakymoData<DATEADD(day,1,@to))))
        """;

    public Task<RoletaiPaperlessData> ReadAsync(string? order, DateOnly? from, DateOnly? to,
        CancellationToken ct) => queue.WithActorConnectionAsync((db, token) => ReadRowsAsync(db, order, from, to, token),
            ct, ProductionQueueReader.ProductionTaskMenuId,
            ProductionQueueReader.FinishedGoodsMenuId);

    public Task<bool> CanMarkAsync(long productId, CancellationToken ct) =>
        queue.WithActorConnectionAsync(async (db, token) =>
        {
            await using var command = new SqlCommand($"""
                SELECT 1 FROM dbo.ParduotiGaminiai pg
                JOIN dbo.Uzsakymai u ON u.UzsakymasID=pg.UzsakymoID
                JOIN dbo.Zinynas_UzsakymoSerijos us ON us.UzsakymoSerijaID=u.UzsakymoSerija
                JOIN dbo.Zinynas_tipai t ON t.TipoID=pg.ParduotoGaminioTipoID
                WHERE pg.ParduotoGaminioID=@id AND pg.BusenosID=1
                  AND ISNULL(u.Sugadintas,0)=0
                  AND ISNULL(u.sNotes,N'')<>N'Kliento prasymu anuliuotas'
                  AND (pg.iCountOfRepairs IS NULL OR pg.iCountOfRepairs=0)
                  AND t.Rusis IN ({Categories})
                  AND EXISTS(SELECT 1 FROM dbo.Loc_leidimas() acl WHERE acl.UKS=us.FilialoID);
                """, db);
            command.Parameters.Add("@id", SqlDbType.BigInt).Value = productId;
            return await command.ExecuteScalarAsync(token) is not null;
        }, ct, ProductionQueueReader.ProductionTaskMenuId,
            ProductionQueueReader.FinishedGoodsMenuId);

    private static async Task<RoletaiPaperlessData> ReadRowsAsync(SqlConnection db, string? order,
        DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var search = string.IsNullOrWhiteSpace(order) ? null :
            new string(order.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        var vilniusToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Vilnius")));
        DateOnly? start = search is null ? from ?? vilniusToday.AddDays(-14) : null;
        DateOnly? end = search is null ? to ?? vilniusToday : null;
        if (search is null && start > end)
            throw new ProductionQueueAccessException("INVALID_DATE_RANGE", "Invalid date range.", 400);
        var status = search is null ? "default" : await OrderStatusAsync(db, search, ct);
        if (status is "notFound" or "cancelled")
            return new RoletaiPaperlessData(status, start, end, [], [], [], []);
        await using var command = new SqlCommand($"""
            SELECT TOP (5001) {OrderNumber} AS Uzsakymas,u.UzsakymoData,
              pg.ParduotoGaminioID,t.TipoTrPavadinimas,pg.Valdymas,t.Rusis,
              pg.Plotis,pg.Aukstis,pg.Kiekis,c.sCodeInternal AS Spalva,
              pg.PPP01 AS Audinys,pg.PPP02 AS Kasete,pg.PPP03 AS Velenas,
              pg.PPP04 AS Atsvaras,pg.PPP05 AS Kreipianciosios,
              pg.PPP06 AS Krepianciuju_tipas,pg.PPP07 AS Dekoras,
              pg.PPP12 AS Audinio_sunaudojimas,
              CASE WHEN t.Rusis=1170057273 THEN N'Versti! '+ISNULL(CONVERT(nvarchar(500),pg.Pastabos),N'')
                   ELSE ISNULL(CONVERT(nvarchar(500),pg.Pastabos),N'') END AS Pastabos,
              t.iCechas AS Cechas,task.sProductionOutputDocumentNo AS GamibinesUzduotiesNumeris,
              CONVERT(bit,CASE WHEN pg.BusenosID=1 THEN 1 ELSE 0 END) AS InProduction
            {BaseFrom}
            ORDER BY t.iCechas,{OrderNumber},c.sCodeInternal,pg.ParduotoGaminioID;
            """, db) { CommandTimeout = 60 };
        AddFilters(command, search, start, end);
        var result = new List<RoletaiPaperlessRow>();
        await using (var rows = await command.ExecuteReaderAsync(ct))
            while (await rows.ReadAsync(ct))
            {
                if (result.Count == 5000)
                    throw new ProductionQueueAccessException("ROLETai_TOO_LARGE", "Narrow the date filter.", 413);
                string? S(int i) => rows.IsDBNull(i) ? null : Convert.ToString(rows.GetValue(i));
                decimal? D(int i) => rows.IsDBNull(i) ? null : Convert.ToDecimal(rows.GetValue(i));
                result.Add(new RoletaiPaperlessRow(rows.GetString(0),rows.GetDateTime(1),
                    Convert.ToInt64(rows.GetValue(2)),S(3) ?? "",S(4),Convert.ToInt64(rows.GetValue(5)),
                    D(6),D(7),D(8) ?? 0,S(9),S(10),S(11),S(12),S(13),S(14),S(15),
                    S(16),S(17),S(18),S(19),S(20),rows.GetBoolean(21)));
            }
        if (search is not null && result.Count == 0) status = "noRollers";
        var accessories = result.Count == 0 ? [] : await AccessoriesAsync(db, search, start, end, ct);
        return new RoletaiPaperlessData(status,start,end,result,accessories,[],[]);
    }

    private static async Task<string> OrderStatusAsync(SqlConnection db, string search, CancellationToken ct)
    {
        await using var command = new SqlCommand($"""
            SELECT COUNT(*),SUM(CASE WHEN ISNULL(u.Sugadintas,0)=0
                AND ISNULL(u.sNotes,N'')<>N'Kliento prasymu anuliuotas' THEN 1 ELSE 0 END)
            FROM dbo.Uzsakymai u JOIN dbo.Zinynas_UzsakymoSerijos us
              ON us.UzsakymoSerijaID=u.UzsakymoSerija
            WHERE EXISTS(SELECT 1 FROM dbo.Loc_leidimas() acl WHERE acl.UKS=us.FilialoID)
              AND CHARINDEX(@order,UPPER(REPLACE({OrderNumber},N' ',N'')))>0;
            """, db);
        command.Parameters.Add("@order", SqlDbType.NVarChar, 80).Value = search;
        await using var rows = await command.ExecuteReaderAsync(ct);
        if (!await rows.ReadAsync(ct) || rows.GetInt32(0)==0) return "notFound";
        return rows.IsDBNull(1) || rows.GetInt32(1)==0 ? "cancelled" : "found";
    }

    private static async Task<IReadOnlyList<ProductionReportAccessory>> AccessoriesAsync(
        SqlConnection db, string? search, DateOnly? start, DateOnly? end, CancellationToken ct)
    {
        await using var command = new SqlCommand($"""
            SELECT a.ParduotoGaminioID,a.Quantity,a.Unit,a.AccessoryName
            FROM dbo.V_ParduotiPriedai a
            WHERE EXISTS (
              SELECT 1 {BaseFrom}
                AND pg.ParduotoGaminioID=a.ParduotoGaminioID)
            ORDER BY a.ParduotoGaminioID,a.AccessoryName;
            """, db) { CommandTimeout = 60 };
        AddFilters(command, search, start, end);
        var result = new List<ProductionReportAccessory>();
        await using var rows = await command.ExecuteReaderAsync(ct);
        while (await rows.ReadAsync(ct))
            result.Add(new ProductionReportAccessory(Convert.ToInt64(rows.GetValue(0)),
                rows.IsDBNull(1) ? 0 : Convert.ToDecimal(rows.GetValue(1)),
                rows.IsDBNull(2) ? null : Convert.ToString(rows.GetValue(2)),
                rows.IsDBNull(3) ? null : Convert.ToString(rows.GetValue(3))));
        return result;
    }

    private static void AddFilters(SqlCommand command, string? search, DateOnly? start, DateOnly? end)
    {
        command.Parameters.Add("@order", SqlDbType.NVarChar, 80).Value = (object?)search ?? DBNull.Value;
        command.Parameters.Add("@from", SqlDbType.Date).Value = start?.ToDateTime(TimeOnly.MinValue) ?? (object)DBNull.Value;
        command.Parameters.Add("@to", SqlDbType.Date).Value = end?.ToDateTime(TimeOnly.MinValue) ?? (object)DBNull.Value;
    }
}

public sealed class RoletaiPaperlessStore(ShopfloorDbContext db)
{
    public async Task<(IReadOnlyList<RoletaiPaperlessMark>,IReadOnlyList<RoletaiPaperlessSignOff>)>
        ReadAsync(HashSet<long> ids, Guid actor, CancellationToken ct)
    {
        if (ids.Count == 0) return ([],[]);
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = (NpgsqlConnection)db.Database.GetDbConnection();
            var all = ids.ToArray();
            var marks = new List<RoletaiPaperlessMark>();
            await using (var command = new NpgsqlCommand("""
                SELECT product_id,step,is_marked,updated_at
                FROM shopfloor.roletai_item_marks
                WHERE product_id=ANY(@ids) AND user_id=@actor
                """, connection))
            {
                command.Parameters.AddWithValue("ids", all);
                command.Parameters.AddWithValue("actor", actor);
                await using var rows = await command.ExecuteReaderAsync(ct);
                while (await rows.ReadAsync(ct))
                    marks.Add(new(rows.GetInt64(0),rows.GetInt16(1),rows.GetBoolean(2),rows.GetFieldValue<DateTimeOffset>(3)));
            }
            var signOffs = new List<RoletaiPaperlessSignOff>();
            await using (var command = new NpgsqlCommand("""
                SELECT product_id,user_id,user_display_name,signed_at
                FROM shopfloor.roletai_item_signoffs
                WHERE product_id=ANY(@ids) AND withdrawn_at IS NULL
                ORDER BY product_id,signed_at
                """, connection))
            {
                command.Parameters.AddWithValue("ids", all);
                await using var rows = await command.ExecuteReaderAsync(ct);
                while (await rows.ReadAsync(ct))
                {
                    var user = rows.GetGuid(1);
                    signOffs.Add(new(rows.GetInt64(0),user,rows.GetString(2),rows.GetFieldValue<DateTimeOffset>(3),user==actor));
                }
            }
            return (marks,signOffs);
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }

    public async Task<RoletaiPaperlessMark> SetMarkAsync(long id, Guid actor, int step,
        bool marked, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = (NpgsqlConnection)db.Database.GetDbConnection();
            await using var command = new NpgsqlCommand("""
                INSERT INTO shopfloor.roletai_item_marks(product_id,user_id,step,is_marked,updated_at)
                VALUES(@id,@actor,@step,@marked,now())
                ON CONFLICT(product_id,user_id,step) DO UPDATE
                SET is_marked=EXCLUDED.is_marked,updated_at=now()
                RETURNING updated_at;
                """, connection);
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("actor", actor);
            command.Parameters.AddWithValue("step", (short)step);
            command.Parameters.AddWithValue("marked", marked);
            var at = (DateTimeOffset)(await command.ExecuteScalarAsync(ct))!;
            return new(id,step,marked,at);
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }

    public async Task<RoletaiPaperlessSignOff> SignAsync(long id, Guid actor,
        string name, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = (NpgsqlConnection)db.Database.GetDbConnection();
            await using var command = new NpgsqlCommand("""
                INSERT INTO shopfloor.roletai_item_signoffs(product_id,user_id,user_display_name,signed_at)
                VALUES(@id,@actor,@name,now())
                ON CONFLICT(product_id,user_id) WHERE withdrawn_at IS NULL
                DO UPDATE SET user_display_name=EXCLUDED.user_display_name
                RETURNING signed_at,user_display_name;
                """, connection);
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("actor", actor);
            command.Parameters.AddWithValue("name", name);
            await using var rows = await command.ExecuteReaderAsync(ct);
            if (!await rows.ReadAsync(ct)) throw new DataException("Sign-off was not saved.");
            return new(id,actor,rows.GetString(1),rows.GetFieldValue<DateTimeOffset>(0),true);
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }

    public async Task WithdrawAsync(long id, Guid actor, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = (NpgsqlConnection)db.Database.GetDbConnection();
            await using var command = new NpgsqlCommand("""
                UPDATE shopfloor.roletai_item_signoffs SET withdrawn_at=now()
                WHERE product_id=@id AND user_id=@actor AND withdrawn_at IS NULL;
                """, connection);
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("actor", actor);
            await command.ExecuteNonQueryAsync(ct);
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}
