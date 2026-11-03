using System.Data;
using Microsoft.Data.SqlClient;

namespace LKvitai.MES.Modules.Shopfloor.Api.Endpoints;

/// <summary>Read-only Access GT document list and allowed-operation catalog.</summary>
public static class ProductionDocumentEndpoints
{
    public static RouteGroupBuilder MapProductionDocumentEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/production-documents", async (int? page, int? pageSize,
            ProductionDocumentReader reader, CancellationToken ct) =>
        {
            if (page is < 1 or > 100000 || pageSize is < 1 or > 100)
                return Results.BadRequest(new { code = "INVALID_DOCUMENT_PAGE" });
            try { return Results.Ok(await reader.ListAsync(page ?? 1, pageSize ?? 50, ct)); }
            catch (ProductionQueueAccessException ex) { return ProductionReadErrors.Access(ex); }
            catch (SqlException ex) when (ex.Number is 229 or 230) { return ProductionReadErrors.PermissionDenied(); }
        });
        group.MapGet("/production-documents/allowed-states", async (
            ProductionDocumentReader reader, CancellationToken ct) =>
        {
            try { return Results.Ok(await reader.AllowedStatesAsync(ct)); }
            catch (ProductionQueueAccessException ex) { return ProductionReadErrors.Access(ex); }
            catch (SqlException ex) when (ex.Number is 229 or 230) { return ProductionReadErrors.PermissionDenied(); }
        });
        group.MapGet("/production-documents/{id:long}", async (long id,
            ProductionDocumentReader reader, CancellationToken ct) =>
        {
            if (id <= 0) return Results.BadRequest(new { code = "INVALID_DOCUMENT_ID" });
            try
            {
                var document = await reader.DetailAsync(id, ct);
                return document is null ? Results.NotFound() : Results.Ok(document);
            }
            catch (ProductionQueueAccessException ex) { return ProductionReadErrors.Access(ex); }
            catch (SqlException ex) when (ex.Number is 229 or 230) { return ProductionReadErrors.PermissionDenied(); }
        });
        return group;
    }
}

public sealed record ProductionDocumentSummary(int Id, string Number, DateTime Date,
    string Author, string Operation, int Rows);
public sealed record ProductionDocumentPage(int Page, int PageSize, bool HasMore,
    IReadOnlyList<ProductionDocumentSummary> Items);
public sealed record ProductionAllowedState(long Id, string Name);
public sealed record ProductionDocumentLine(string Barcode, string? Description, string? Before,
    string? After, string? InsertedBy, DateTime? InsertedAt);
public sealed record ProductionDocumentDetail(ProductionDocumentSummary Document,
    IReadOnlyList<ProductionDocumentLine> Items);

public sealed class ProductionDocumentReader(ProductionQueueReader queue)
{
    public Task<ProductionDocumentPage> ListAsync(int page, int pageSize, CancellationToken ct)
        => queue.WithActorConnectionAsync((connection, token) => ListRowsAsync(connection, page, pageSize, token),
            ct, ProductionQueueReader.FinishedGoodsMenuId);

    public Task<IReadOnlyList<ProductionAllowedState>> AllowedStatesAsync(CancellationToken ct)
        => queue.WithActorConnectionAsync(ReadAllowedStatesAsync, ct,
            ProductionQueueReader.FinishedGoodsMenuId);

    public Task<ProductionDocumentDetail?> DetailAsync(long id, CancellationToken ct)
        => queue.WithActorConnectionAsync((connection, token) => ReadDetailAsync(connection, id, token),
            ct, ProductionQueueReader.FinishedGoodsMenuId);

    private static async Task<ProductionDocumentPage> ListRowsAsync(SqlConnection connection,
        int page, int pageSize, CancellationToken ct)
    {
        await using var command = new SqlCommand(DocumentListSql, connection) { CommandTimeout = 30 };
        command.Parameters.Add("@offset", SqlDbType.Int).Value = (page - 1) * pageSize;
        command.Parameters.Add("@take", SqlDbType.Int).Value = pageSize + 1;
        var items = new List<ProductionDocumentSummary>(pageSize + 1);
        await using var rows = await command.ExecuteReaderAsync(ct);
        while (await rows.ReadAsync(ct))
            items.Add(MapSummary(rows));
        var hasMore = items.Count > pageSize;
        if (hasMore) items.RemoveAt(pageSize);
        return new ProductionDocumentPage(page, pageSize, hasMore, items);
    }

    private static async Task<IReadOnlyList<ProductionAllowedState>> ReadAllowedStatesAsync(
        SqlConnection connection, CancellationToken ct)
    {
        await using var command = new SqlCommand("dbo.sp_cbo_AssignedStateID_dlgNewProductionOutputDocument",
            connection) { CommandType = CommandType.StoredProcedure, CommandTimeout = 30 };
        var states = new List<ProductionAllowedState>();
        await using var rows = await command.ExecuteReaderAsync(ct);
        while (await rows.ReadAsync(ct))
            states.Add(new ProductionAllowedState(Convert.ToInt64(rows.GetValue(0)), rows.GetString(1)));
        return states;
    }

    private static async Task<ProductionDocumentDetail?> ReadDetailAsync(SqlConnection connection,
        long id, CancellationToken ct)
    {
        await using var header = new SqlCommand(DocumentDetailSql, connection) { CommandTimeout = 30 };
        header.Parameters.Add("@id", SqlDbType.BigInt).Value = id;
        ProductionDocumentSummary? document = null;
        await using (var row = await header.ExecuteReaderAsync(ct))
            if (await row.ReadAsync(ct)) document = MapSummary(row);
        if (document is null) return null;
        await using var lines = new SqlCommand(DocumentLinesSql, connection) { CommandTimeout = 30 };
        lines.Parameters.Add("@id", SqlDbType.BigInt).Value = id;
        var items = new List<ProductionDocumentLine>();
        await using var rows = await lines.ExecuteReaderAsync(ct);
        while (await rows.ReadAsync(ct))
            items.Add(new ProductionDocumentLine(rows.GetString(0),
                rows.IsDBNull(1) ? null : rows.GetString(1),
                rows.IsDBNull(2) ? null : rows.GetString(2),
                rows.IsDBNull(3) ? null : rows.GetString(3),
                rows.IsDBNull(4) ? null : rows.GetString(4),
                rows.IsDBNull(5) ? null : rows.GetDateTime(5)));
        // A concurrent Access scan may change the composition after the header read.
        // The line query only returns authorized products; a mismatch fails closed.
        return items.Count == document.Rows ? new ProductionDocumentDetail(document, items) : null;
    }

    private static ProductionDocumentSummary MapSummary(SqlDataReader row) => new(
        row.GetInt32(0), row.GetString(1), row.GetDateTime(2), row.GetString(3),
        row.IsDBNull(4) ? "" : row.GetString(4), row.GetInt32(5));

    // Same author and date window as frmMadeProductionDocuments.MakelstDocs.
    // Empty own documents are visible; a document with any inaccessible product
    // is hidden in full, including its header and row count.
    private const string DocumentListSql = """
        SELECT d.iProductionOutputDocumentID, d.sProductionOutputDocumentNo,
               d.dtDateTime, d.sUserNameInsert, b.sNameInProductionOutputDocs,
               (SELECT COUNT(*) FROM dbo.TAB_ProductionOutputDocumentDetails n
                 WHERE n.iProductionOutputDocumentID=d.iProductionOutputDocumentID)
        FROM dbo.TAB_ProductionOutputDocuments d
        JOIN dbo.Zinynas_busenos b ON b.BusenosID=d.iAssignedStatusID
        WHERE LEFT(d.sProductionOutputDocumentNo,2)=N'GT'
          AND d.sUserNameInsert=SYSTEM_USER
          AND YEAR(d.dtDateTime)=YEAR(GETDATE())
          AND MONTH(d.dtDateTime)=MONTH(GETDATE())
          AND DAY(d.dtDateTime)>DAY(GETDATE())-4
          AND NOT EXISTS (
            SELECT 1 FROM dbo.TAB_ProductionOutputDocumentDetails hidden
            LEFT JOIN dbo.ParduotiGaminiai pg ON pg.iBarCode=hidden.iBarCode
            LEFT JOIN dbo.Uzsakymai u ON u.UzsakymasID=pg.UzsakymoID
            LEFT JOIN dbo.Zinynas_UzsakymoSerijos s ON s.UzsakymoSerijaID=u.UzsakymoSerija
            LEFT JOIN dbo.Loc_leidimas() acl ON acl.UKS=s.FilialoID
            WHERE hidden.iProductionOutputDocumentID=d.iProductionOutputDocumentID
              AND acl.UKS IS NULL)
        ORDER BY d.dtDateInsert DESC, d.iProductionOutputDocumentID DESC
        OFFSET @offset ROWS FETCH NEXT @take ROWS ONLY;
        """;

    private const string DocumentDetailSql = """
        SELECT d.iProductionOutputDocumentID, d.sProductionOutputDocumentNo,
               d.dtDateTime, d.sUserNameInsert, b.sNameInProductionOutputDocs,
               (SELECT COUNT(*) FROM dbo.TAB_ProductionOutputDocumentDetails n
                 WHERE n.iProductionOutputDocumentID=d.iProductionOutputDocumentID)
        FROM dbo.TAB_ProductionOutputDocuments d
        JOIN dbo.Zinynas_busenos b ON b.BusenosID=d.iAssignedStatusID
        WHERE d.iProductionOutputDocumentID=@id
          AND LEFT(d.sProductionOutputDocumentNo,2)=N'GT'
          AND d.sUserNameInsert=SYSTEM_USER
          AND YEAR(d.dtDateTime)=YEAR(GETDATE())
          AND MONTH(d.dtDateTime)=MONTH(GETDATE())
          AND DAY(d.dtDateTime)>DAY(GETDATE())-4
          AND NOT EXISTS (
            SELECT 1 FROM dbo.TAB_ProductionOutputDocumentDetails hidden
            LEFT JOIN dbo.ParduotiGaminiai pg ON pg.iBarCode=hidden.iBarCode
            LEFT JOIN dbo.Uzsakymai u ON u.UzsakymasID=pg.UzsakymoID
            LEFT JOIN dbo.Zinynas_UzsakymoSerijos s ON s.UzsakymoSerijaID=u.UzsakymoSerija
            LEFT JOIN dbo.Loc_leidimas() acl ON acl.UKS=s.FilialoID
            WHERE hidden.iProductionOutputDocumentID=d.iProductionOutputDocumentID
              AND acl.UKS IS NULL);
        """;

    private const string DocumentLinesSql = """
        SELECT n.iBarCode, n.sItemDescription, beforeState.Busena, afterState.Busena,
               n.sUserNameInsert, n.dtDateInsert
        FROM dbo.TAB_ProductionOutputDocumentDetails n
        JOIN dbo.ParduotiGaminiai pg ON pg.iBarCode=n.iBarCode
        JOIN dbo.Uzsakymai u ON u.UzsakymasID=pg.UzsakymoID
        JOIN dbo.Zinynas_UzsakymoSerijos s ON s.UzsakymoSerijaID=u.UzsakymoSerija
        LEFT JOIN dbo.Zinynas_busenos beforeState ON beforeState.BusenosID=n.iStateBefore
        LEFT JOIN dbo.Zinynas_busenos afterState ON afterState.BusenosID=n.iStateAfter
        WHERE n.iProductionOutputDocumentID=@id
          AND EXISTS (SELECT 1 FROM dbo.Loc_leidimas() acl WHERE acl.UKS=s.FilialoID)
        ORDER BY n.dtDateInsert, n.iBarCode;
        """;
}
