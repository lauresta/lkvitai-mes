using System.Data;
using Microsoft.Data.SqlClient;

namespace LKvitai.MES.Modules.Shopfloor.Api.Endpoints;

public static class ProductionHistoryEndpoints
{
    public static RouteGroupBuilder MapProductionHistoryEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/production-products/{barcode}/history", async (string barcode,
            ProductionHistoryReader reader, CancellationToken ct) =>
        {
            if (barcode.Length != 12 || barcode.Any(c => c is < '0' or > '9'))
                return Results.BadRequest(new { code = "INVALID_BARCODE" });
            try
            {
                var history = await reader.ReadAsync(barcode, ct);
                return history is null ? Results.NotFound() : Results.Ok(history);
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

public sealed record ProductionHistoryDocument(int Id, string Number, DateTime? Date,
    string? Author, string? Before, string? After);
public sealed record ProductionProductHistory(long ProductId, string Barcode, string OrderNumber,
    DateTime OrderDate, string Department, string? Customer, string Category,
    string ProductType, string? Control, string? MaterialCode, decimal? WidthMm,
    decimal? HeightMm, decimal Quantity, string? Notes, string State,
    IReadOnlyList<ProductionHistoryDocument> Documents);

public sealed class ProductionHistoryReader(ProductionQueueReader queue)
{
    public Task<ProductionProductHistory?> ReadAsync(string barcode, CancellationToken ct)
        => queue.WithActorConnectionAsync((connection, token) => ReadRowsAsync(connection, barcode, token), ct);

    private static async Task<ProductionProductHistory?> ReadRowsAsync(SqlConnection connection,
        string barcode, CancellationToken ct)
    {
        await using var product = new SqlCommand(ProductSql, connection) { CommandTimeout = 30 };
        product.Parameters.Add("@barcode", SqlDbType.NVarChar, 12).Value = barcode;
        ProductionProductHistory? result;
        await using (var row = await product.ExecuteReaderAsync(ct))
        {
            if (!await row.ReadAsync(ct)) return null;
            result = new ProductionProductHistory(Convert.ToInt64(row.GetValue(0)), barcode,
                row.GetString(1), row.GetDateTime(2), row.GetString(3),
                row.IsDBNull(4) ? null : row.GetString(4), row.GetString(5), row.GetString(6),
                row.IsDBNull(7) ? null : row.GetString(7), row.IsDBNull(8) ? null : row.GetString(8),
                row.IsDBNull(9) ? null : Convert.ToDecimal(row.GetValue(9)),
                row.IsDBNull(10) ? null : Convert.ToDecimal(row.GetValue(10)), row.GetDecimal(11),
                row.IsDBNull(12) ? null : row.GetString(12), row.GetString(13), []);
        }
        await using var documents = new SqlCommand(DocumentsSql, connection) { CommandTimeout = 30 };
        documents.Parameters.Add("@barcode", SqlDbType.NVarChar, 12).Value = barcode;
        var timeline = new List<ProductionHistoryDocument>();
        await using var rows = await documents.ExecuteReaderAsync(ct);
        while (await rows.ReadAsync(ct))
            timeline.Add(new ProductionHistoryDocument(rows.GetInt32(0),
                rows.IsDBNull(1) ? "—" : rows.GetString(1),
                rows.IsDBNull(2) ? null : rows.GetDateTime(2),
                rows.IsDBNull(3) ? null : rows.GetString(3),
                rows.IsDBNull(4) ? null : rows.GetString(4),
                rows.IsDBNull(5) ? null : rows.GetString(5)));
        return result with { Documents = timeline };
    }

    private const string ProductSql = """
        SELECT TOP(1) pg.ParduotoGaminioID,
               s.UzsakymoSerijaPav+N'-'+CONVERT(nvarchar(15),u.UzsakymoNumeris)+N'-'+u.UzsakymoGrupe,
               u.UzsakymoData, f.FilialoPavadinimas, uzs.UzsakovoPavadinimas,
               r.RusiesPavadinimas, t.TipoPavadinimas, pg.Valdymas, c.sCodeInternal,
               pg.Plotis, pg.Aukstis, pg.Kiekis, pg.Pastabos, b.Busena
        FROM dbo.ParduotiGaminiai pg
        JOIN dbo.Uzsakymai u ON u.UzsakymasID=pg.UzsakymoID
        JOIN dbo.Zinynas_UzsakymoSerijos s ON s.UzsakymoSerijaID=u.UzsakymoSerija
        JOIN dbo.Zinynas_filialai f ON f.FilialoID=s.FilialoID
        JOIN dbo.Zinynas_uzsakovai uzs ON uzs.UzsakovoID=u.UzsakovoID
        JOIN dbo.Zinynas_tipai t ON t.TipoID=pg.ParduotoGaminioTipoID
        JOIN dbo.Zinynas_rusys r ON r.RusiesID=t.Rusis
        JOIN dbo.Zinynas_busenos b ON b.BusenosID=pg.BusenosID
        LEFT JOIN dbo.TBD_Components c ON c.iComponentID=pg.Spalva
        WHERE pg.iBarCode=@barcode
          AND EXISTS(SELECT 1 FROM dbo.Loc_leidimas() acl WHERE acl.UKS=s.FilialoID);
        """;

    private static readonly string DocumentsSql = $"""
        SELECT d.iProductionOutputDocumentID, d.sProductionOutputDocumentNo,
               d.dtDateTime, d.sUserNameInsert, beforeState.Busena, afterState.Busena
        FROM dbo.TAB_ProductionOutputDocumentDetails item
        JOIN dbo.TAB_ProductionOutputDocuments d
          ON d.iProductionOutputDocumentID=item.iProductionOutputDocumentID
        LEFT JOIN dbo.Zinynas_busenos beforeState ON beforeState.BusenosID=item.iStateBefore
        LEFT JOIN dbo.Zinynas_busenos afterState ON afterState.BusenosID=item.iStateAfter
        WHERE item.iBarCode=@barcode AND {ProductionTaskReader.Visibility}
        ORDER BY d.dtDateTime, d.iProductionOutputDocumentID;
        """;
}
