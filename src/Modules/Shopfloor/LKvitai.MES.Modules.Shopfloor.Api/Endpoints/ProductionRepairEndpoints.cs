using System.Data;
using Microsoft.Data.SqlClient;

namespace LKvitai.MES.Modules.Shopfloor.Api.Endpoints;

public static class ProductionRepairEndpoints
{
    public static RouteGroupBuilder MapProductionRepairEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/production-repairs", async (int? page, string? filter,
            ProductionRepairReader reader, CancellationToken ct) =>
        {
            if (page is < 1 or > 100000 || filter is not (null or "all" or "unfinished" or "unknown"))
                return Results.BadRequest(new { code = "INVALID_REPAIR_FILTER" });
            try { return Results.Ok(await reader.ReadAsync(page ?? 1, filter ?? "all", ct)); }
            catch (ProductionQueueAccessException ex)
            {
                return Results.Problem(ex.Message, statusCode: ex.Status,
                    extensions: new Dictionary<string, object?> { ["code"] = ex.Code });
            }
            catch (SqlException ex) when (ex.Number is 229 or 230)
            {
                return Results.Problem("Your legacy SQL account does not permit this operation.",
                    statusCode: 403, extensions: new Dictionary<string, object?>
                    { ["code"] = "LEGACY_SQL_PERMISSION_DENIED" });
            }
        });
        group.MapGet("/production-repairs/{id:int}", async (int id,
            ProductionRepairReader reader, CancellationToken ct) =>
        {
            if (id < 1) return Results.BadRequest(new { code = "INVALID_REPAIR_ID" });
            try
            {
                var repair = await reader.ReadOneAsync(id, ct);
                return repair is null ? Results.NotFound() : Results.Ok(repair);
            }
            catch (ProductionQueueAccessException ex)
            {
                return Results.Problem(ex.Message, statusCode: ex.Status,
                    extensions: new Dictionary<string, object?> { ["code"] = ex.Code });
            }
            catch (SqlException ex) when (ex.Number is 229 or 230)
            {
                return Results.Problem("Your legacy SQL account does not permit this operation.",
                    statusCode: 403, extensions: new Dictionary<string, object?>
                    { ["code"] = "LEGACY_SQL_PERMISSION_DENIED" });
            }
        });
        return group;
    }
}

public sealed record ProductionRepair(int Id, long ProductId, string Barcode, string OrderNumber,
    string ProductType, decimal? WidthMm, decimal? HeightMm, DateTime Registered,
    string Reason, string? Claimed, string? Actual, string ProductState,
    bool Unfinished, bool MissingActual);
public sealed record ProductionRepairPage(int Page, bool HasMore, IReadOnlyList<ProductionRepair> Items);

public sealed class ProductionRepairReader(ProductionQueueReader queue)
{
    public Task<ProductionRepairPage> ReadAsync(int page, string filter, CancellationToken ct)
        => queue.WithActorConnectionAsync((connection, token) => ReadRowsAsync(connection, page, filter, token), ct);

    public Task<ProductionRepair?> ReadOneAsync(int id, CancellationToken ct)
        => queue.WithActorConnectionAsync((connection, token) => ReadOneRowAsync(connection, id, token), ct);

    private static async Task<ProductionRepairPage> ReadRowsAsync(SqlConnection connection,
        int page, string filter, CancellationToken ct)
    {
        await using var command = new SqlCommand($"""
            {SourceSql}
            AND (@filter=N'all'
              OR (@filter=N'unfinished' AND pg.BusenosID IN (1173102377,1,1173102374))
              OR (@filter=N'unknown' AND NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(max),rep.sFactFailure))),N'') IS NULL))
            ORDER BY rep.dtDateTime DESC,rep.iRepairmentID DESC
            OFFSET @offset ROWS FETCH NEXT 51 ROWS ONLY;
            """, connection) { CommandTimeout = 30 };
        command.Parameters.Add("@filter", SqlDbType.NVarChar, 16).Value = filter;
        command.Parameters.Add("@offset", SqlDbType.Int).Value = checked((page - 1) * 50);
        var items = new List<ProductionRepair>();
        await using var rows = await command.ExecuteReaderAsync(ct);
        while (await rows.ReadAsync(ct)) items.Add(Map(rows));
        return new ProductionRepairPage(page, items.Count > 50, items.Take(50).ToArray());
    }

    private static async Task<ProductionRepair?> ReadOneRowAsync(SqlConnection connection,
        int id, CancellationToken ct)
    {
        await using var command = new SqlCommand($"""
            {SourceSql}
            AND rep.iRepairmentID=@id;
            """, connection) { CommandTimeout = 30 };
        command.Parameters.Add("@id", SqlDbType.Int).Value = id;
        await using var row = await command.ExecuteReaderAsync(ct);
        return await row.ReadAsync(ct) ? Map(row) : null;
    }

    private static ProductionRepair Map(SqlDataReader row)
    {
        var stateId = row.GetInt32(12);
        var actual = row.IsDBNull(10) ? null : row.GetString(10);
        return new ProductionRepair(row.GetInt32(0), Convert.ToInt64(row.GetValue(1)),
            row.GetString(2), row.GetString(3), row.GetString(4),
            row.IsDBNull(5) ? null : Convert.ToDecimal(row.GetValue(5)),
            row.IsDBNull(6) ? null : Convert.ToDecimal(row.GetValue(6)),
            row.GetDateTime(7), row.IsDBNull(8) ? "—" : row.GetString(8),
            row.IsDBNull(9) ? null : row.GetString(9), actual,
            row.GetString(11), stateId is 1173102377 or 1 or 1173102374,
            string.IsNullOrWhiteSpace(actual));
    }

    private const string SourceSql = """
        SELECT rep.iRepairmentID,pg.ParduotoGaminioID,pg.iBarCode,
               s.UzsakymoSerijaPav+N'-'+CONVERT(nvarchar(15),u.UzsakymoNumeris)+N'-'+u.UzsakymoGrupe,
               t.TipoPavadinimas,pg.Plotis,pg.Aukstis,rep.dtDateTime,
               reason.sReason,CONVERT(nvarchar(max),rep.sFailureDescription),
               CONVERT(nvarchar(max),rep.sFactFailure),state.Busena,pg.BusenosID
        FROM dbo.TAB_BlindRepairments rep
        JOIN dbo.ParduotiGaminiai pg ON pg.ParduotoGaminioID=rep.iBlindID
        JOIN dbo.Uzsakymai u ON u.UzsakymasID=pg.UzsakymoID
        JOIN dbo.Zinynas_UzsakymoSerijos s ON s.UzsakymoSerijaID=u.UzsakymoSerija
        JOIN dbo.Zinynas_tipai t ON t.TipoID=pg.ParduotoGaminioTipoID
        JOIN dbo.Zinynas_busenos state ON state.BusenosID=pg.BusenosID
        LEFT JOIN dbo.TBD_RepairmentReasons reason ON reason.iReasonID=rep.iReasonID
        WHERE EXISTS(SELECT 1 FROM dbo.Loc_leidimas() acl WHERE acl.UKS=s.FilialoID)
        """;
}
