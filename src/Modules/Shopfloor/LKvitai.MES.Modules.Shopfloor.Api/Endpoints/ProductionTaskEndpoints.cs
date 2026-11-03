using System.Data;
using Microsoft.Data.SqlClient;

namespace LKvitai.MES.Modules.Shopfloor.Api.Endpoints;

/// <summary>Read-only GU documents. A task with any hidden or unresolved item is omitted.</summary>
public static class ProductionTaskEndpoints
{
    public static RouteGroupBuilder MapProductionTaskEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/production-tasks", async (int? page, int? pageSize,
            ProductionTaskReader reader, CancellationToken ct) =>
        {
            if (page is < 1 or > 100000 || pageSize is < 1 or > 100)
                return Results.BadRequest(new { code = "INVALID_TASK_PAGE" });
            try { return Results.Ok(await reader.ListAsync(page ?? 1, pageSize ?? 50, ct)); }
            catch (ProductionQueueAccessException ex) { return Error(ex); }
            catch (SqlException ex) when (ex.Number is 229 or 230) { return PermissionDenied(); }
        });
        group.MapGet("/production-tasks/{id:long}", async (long id,
            ProductionTaskReader reader, CancellationToken ct) =>
        {
            if (id <= 0) return Results.BadRequest(new { code = "INVALID_TASK_ID" });
            try
            {
                var task = await reader.DetailAsync(id, ct);
                return task is null ? Results.NotFound() : Results.Ok(task);
            }
            catch (ProductionQueueAccessException ex) { return Error(ex); }
            catch (SqlException ex) when (ex.Number is 229 or 230) { return PermissionDenied(); }
        });
        return group;
    }

    private static IResult Error(ProductionQueueAccessException ex) => Results.Problem(ex.Message,
        statusCode: ex.Status, extensions: new Dictionary<string, object?> { ["code"] = ex.Code });
    private static IResult PermissionDenied() => Results.Problem(
        "Your legacy SQL account does not permit this operation.", statusCode: 403,
        extensions: new Dictionary<string, object?> { ["code"] = "LEGACY_SQL_PERMISSION_DENIED" });
}

public sealed record ProductionTaskSummary(int Id, string Number, DateTime? Created,
    string? Author, int ItemRows, int OrderCount);
public sealed record ProductionTaskPage(int Page, int PageSize, bool HasMore,
    IReadOnlyList<ProductionTaskSummary> Items);
public sealed record ProductionTaskLine(long ProductId, string Barcode, string OrderNumber,
    string ProductType, string? MaterialCode, decimal? WidthMm, decimal? HeightMm,
    decimal Quantity, string State);
public sealed record ProductionTaskDetail(ProductionTaskSummary Task,
    IReadOnlyList<ProductionTaskLine> Items);

public sealed class ProductionTaskReader(ProductionQueueReader queue)
{
    public Task<ProductionTaskPage> ListAsync(int page, int pageSize, CancellationToken ct)
        => queue.WithActorConnectionAsync((connection, token) => ListRowsAsync(connection, page, pageSize, token), ct);

    public Task<ProductionTaskDetail?> DetailAsync(long id, CancellationToken ct)
        => queue.WithActorConnectionAsync((connection, token) => DetailRowsAsync(connection, id, token), ct);

    private static async Task<ProductionTaskPage> ListRowsAsync(SqlConnection connection,
        int page, int pageSize, CancellationToken ct)
    {
        await using var command = new SqlCommand(TaskListSql, connection) { CommandTimeout = 30 };
        command.Parameters.Add("@offset", SqlDbType.Int).Value = (page - 1) * pageSize;
        command.Parameters.Add("@take", SqlDbType.Int).Value = pageSize + 1;
        var tasks = new List<ProductionTaskSummary>(pageSize + 1);
        await using var rows = await command.ExecuteReaderAsync(ct);
        while (await rows.ReadAsync(ct)) tasks.Add(MapTask(rows));
        var hasMore = tasks.Count > pageSize;
        if (hasMore) tasks.RemoveAt(pageSize);
        return new ProductionTaskPage(page, pageSize, hasMore, tasks);
    }

    private static async Task<ProductionTaskDetail?> DetailRowsAsync(SqlConnection connection,
        long id, CancellationToken ct)
    {
        await using var header = new SqlCommand(TaskDetailSql, connection) { CommandTimeout = 30 };
        header.Parameters.Add("@id", SqlDbType.BigInt).Value = id;
        ProductionTaskSummary? task;
        await using (var row = await header.ExecuteReaderAsync(ct))
            task = await row.ReadAsync(ct) ? MapTask(row) : null;
        if (task is null) return null;
        await using var lines = new SqlCommand(TaskLinesSql, connection) { CommandTimeout = 30 };
        lines.Parameters.Add("@id", SqlDbType.BigInt).Value = id;
        var items = new List<ProductionTaskLine>();
        await using var rows = await lines.ExecuteReaderAsync(ct);
        while (await rows.ReadAsync(ct))
            items.Add(new ProductionTaskLine(Convert.ToInt64(rows.GetValue(0)), rows.GetString(1),
                rows.GetString(2), rows.GetString(3), rows.IsDBNull(4) ? null : rows.GetString(4),
                rows.IsDBNull(5) ? null : Convert.ToDecimal(rows.GetValue(5)),
                rows.IsDBNull(6) ? null : Convert.ToDecimal(rows.GetValue(6)),
                rows.GetDecimal(7), rows.GetString(8)));
        // A concurrent Access edit may add a hidden row after the header query.
        // Fail closed if the visible composition no longer matches the document.
        if (items.Count != task.ItemRows) return null;
        return new ProductionTaskDetail(task, items);
    }

    private static ProductionTaskSummary MapTask(SqlDataReader row) => new(
        row.GetInt32(0), row.GetString(1), row.IsDBNull(2) ? null : row.GetDateTime(2),
        row.IsDBNull(3) ? null : row.GetString(3), row.GetInt32(4), row.GetInt32(5));

    internal const string Visibility = """
        EXISTS (SELECT 1 FROM dbo.TAB_ProductionOutputDocumentDetails anyrow
                WHERE anyrow.iProductionOutputDocumentID=d.iProductionOutputDocumentID)
        AND NOT EXISTS (
            SELECT 1 FROM dbo.TAB_ProductionOutputDocumentDetails hidden
            LEFT JOIN dbo.ParduotiGaminiai hpg ON hpg.iBarCode=hidden.iBarCode
            LEFT JOIN dbo.Uzsakymai hu ON hu.UzsakymasID=hpg.UzsakymoID
            LEFT JOIN dbo.Zinynas_UzsakymoSerijos hs ON hs.UzsakymoSerijaID=hu.UzsakymoSerija
            LEFT JOIN dbo.Loc_leidimas() ha ON ha.UKS=hs.FilialoID
            WHERE hidden.iProductionOutputDocumentID=d.iProductionOutputDocumentID
              AND ha.UKS IS NULL)
        """;

    private static readonly string TaskListSql = $"""
        SELECT d.iProductionOutputDocumentID, d.sProductionOutputDocumentNo,
               d.dtDateTime, d.sUserNameInsert,
               (SELECT COUNT(*) FROM dbo.TAB_ProductionOutputDocumentDetails n
                 WHERE n.iProductionOutputDocumentID=d.iProductionOutputDocumentID),
               (SELECT COUNT(DISTINCT pg.UzsakymoID)
                  FROM dbo.TAB_ProductionOutputDocumentDetails n
                  JOIN dbo.ParduotiGaminiai pg ON pg.iBarCode=n.iBarCode
                 WHERE n.iProductionOutputDocumentID=d.iProductionOutputDocumentID)
        FROM dbo.TAB_ProductionOutputDocuments d
        WHERE d.sProductionOutputDocumentNo LIKE N'GU%' AND {Visibility}
        ORDER BY d.dtDateTime DESC, d.iProductionOutputDocumentID DESC
        OFFSET @offset ROWS FETCH NEXT @take ROWS ONLY;
        """;

    private static readonly string TaskDetailSql = $"""
        SELECT d.iProductionOutputDocumentID, d.sProductionOutputDocumentNo,
               d.dtDateTime, d.sUserNameInsert,
               (SELECT COUNT(*) FROM dbo.TAB_ProductionOutputDocumentDetails n
                 WHERE n.iProductionOutputDocumentID=d.iProductionOutputDocumentID),
               (SELECT COUNT(DISTINCT pg.UzsakymoID)
                  FROM dbo.TAB_ProductionOutputDocumentDetails n
                  JOIN dbo.ParduotiGaminiai pg ON pg.iBarCode=n.iBarCode
                 WHERE n.iProductionOutputDocumentID=d.iProductionOutputDocumentID)
        FROM dbo.TAB_ProductionOutputDocuments d
        WHERE d.iProductionOutputDocumentID=@id
          AND d.sProductionOutputDocumentNo LIKE N'GU%' AND {Visibility};
        """;

    private const string TaskLinesSql = """
        SELECT pg.ParduotoGaminioID, n.iBarCode,
               s.UzsakymoSerijaPav+N'-'+CONVERT(nvarchar(15),u.UzsakymoNumeris)+N'-'+u.UzsakymoGrupe,
               t.TipoPavadinimas, c.sCodeInternal, pg.Plotis, pg.Aukstis, pg.Kiekis, b.Busena
        FROM dbo.TAB_ProductionOutputDocumentDetails n
        JOIN dbo.ParduotiGaminiai pg ON pg.iBarCode=n.iBarCode
        JOIN dbo.Uzsakymai u ON u.UzsakymasID=pg.UzsakymoID
        JOIN dbo.Zinynas_UzsakymoSerijos s ON s.UzsakymoSerijaID=u.UzsakymoSerija
        JOIN dbo.Zinynas_tipai t ON t.TipoID=pg.ParduotoGaminioTipoID
        JOIN dbo.Zinynas_busenos b ON b.BusenosID=pg.BusenosID
        LEFT JOIN dbo.TBD_Components c ON c.iComponentID=pg.Spalva
        WHERE n.iProductionOutputDocumentID=@id
          AND EXISTS(SELECT 1 FROM dbo.Loc_leidimas() acl WHERE acl.UKS=s.FilialoID)
        ORDER BY n.dtDateInsert, n.iBarCode;
        """;
}
