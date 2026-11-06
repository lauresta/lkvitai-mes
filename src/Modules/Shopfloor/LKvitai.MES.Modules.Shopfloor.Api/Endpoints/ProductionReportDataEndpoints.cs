using System.Data;
using Microsoft.Data.SqlClient;

namespace LKvitai.MES.Modules.Shopfloor.Api.Endpoints;

/// <summary>Read-only legacy datasets for the 13 active GU reports; no PDF or printer operation.</summary>
public static class ProductionReportDataEndpoints
{
    public static RouteGroupBuilder MapProductionReportDataEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/production-reports", async (ProductionReportDataReader reader, CancellationToken ct) =>
        {
            try { return Results.Ok(await reader.CatalogAsync(ct)); }
            catch (ProductionQueueAccessException ex) { return ProductionReadErrors.Access(ex); }
            catch (SqlException ex) when (ex.Number is 229 or 230) { return ProductionReadErrors.PermissionDenied(); }
        });
        group.MapGet("/production-tasks/{taskId:int}/report-data/{reportId:int}",
            async (int taskId, int reportId, ProductionReportDataReader reader, CancellationToken ct) =>
        {
            if (taskId < 1 || reportId < 1) return Results.BadRequest(new { code = "INVALID_REPORT_REQUEST" });
            try
            {
                var data = await reader.ReadAsync(taskId, reportId, ct);
                return data is null ? Results.NotFound() : Results.Ok(data);
            }
            catch (ProductionQueueAccessException ex) { return ProductionReadErrors.Access(ex); }
            catch (SqlException ex) when (ex.Number is 229 or 230) { return ProductionReadErrors.PermissionDenied(); }
        });
        return group;
    }

}

public sealed record ProductionReportCatalogItem(int Id, string Name, string Title);
public sealed record ProductionReportAccessory(long ProductId, decimal Quantity,
    string? Unit, string? AccessoryName);
public sealed record ProductionReportData(int TaskId, int ReportId, string ReportName,
    IReadOnlyList<string> Columns, IReadOnlyList<object?[]> Rows, bool NoData,
    IReadOnlyList<ProductionReportAccessory> Accessories);

public sealed class ProductionReportDataReader(ProductionQueueReader queue)
{
    private sealed record Definition(string Name, string Procedure, bool HasAccessorySubreport = true);
    private static readonly IReadOnlyDictionary<int, Definition> Reports = new Dictionary<int, Definition>
    {
        [1] = new("rptGamDokXLPlise", "dbo.SP_GamPlisuotasByTask"),
        [2] = new("rptGamDokTinkleliai", "dbo.SP_GamTinkleliaiByTask"),
        [3] = new("rptGamDokRoletai", "dbo.SP_GamRoletaiByTask"),
        [4] = new("rptGamDokApsaugines", "dbo.SP_GamApsauginesByTask"),
        [5] = new("rptGamDokHorizontalios", "dbo.SP_GamHorizontaliosByTask"),
        [6] = new("rptGamDokPanelTrack", "dbo.SP_GamPanelTrackByTask"),
        [7] = new("rptGamDokRemontasPriedai", "dbo.SP_GamRemontasPriedaiByTask"),
        [8] = new("rptGamDokVertikalios", "dbo.SP_GamVertikaliosByTask"),
        [9] = new("rptGamDokRomanetes", "dbo.SP_GamRomanetesByTask"),
        [17] = new("rptGamDokGarantiniai", "dbo.SP_GamInWarrantyFaultsByTask", false),
        [18] = new("rptGamDokNatureConcept", "dbo.SP_rptGamNatureConceptByTask"),
        [19] = new("rptGamDokGrotos", "dbo.SP_GamGrotosByTask"),
        // Report_Open replaces its design-time RecordSource with the XL-Plise proc.
        [20] = new("rptGamDokPleated", "dbo.SP_GamPlisuotasByTask")
    };

    public Task<IReadOnlyList<ProductionReportCatalogItem>> CatalogAsync(CancellationToken ct)
        => queue.WithActorConnectionAsync(CatalogRowsAsync, ct,
            ProductionQueueReader.ProductionTaskMenuId, ProductionQueueReader.FinishedGoodsMenuId);

    public Task<ProductionReportData?> ReadAsync(int taskId, int reportId, CancellationToken ct)
        => queue.WithActorConnectionAsync((connection, token) => ReadRowsAsync(connection, taskId, reportId, token),
            ct, ProductionQueueReader.ProductionTaskMenuId, ProductionQueueReader.FinishedGoodsMenuId);

    private static async Task<IReadOnlyList<ProductionReportCatalogItem>> CatalogRowsAsync(
        SqlConnection connection, CancellationToken ct)
    {
        await using var command = new SqlCommand("""
            SELECT iReportID,sName,sFriedlyName FROM dbo.DIC_ProjectReports
            WHERE sCategory=N'gam' ORDER BY iReportID;
            """, connection);
        var result = new List<ProductionReportCatalogItem>();
        await using var rows = await command.ExecuteReaderAsync(ct);
        while (await rows.ReadAsync(ct))
        {
            var id = rows.GetInt32(0);
            var name = rows.GetString(1);
            if (Reports.TryGetValue(id, out var report) && name == report.Name)
                result.Add(new ProductionReportCatalogItem(id, name, rows.GetString(2)));
        }
        // A calculated document is a Shopfloor report, not an Access report row.
        // Keep its stable synthetic id outside the legacy DIC_ProjectReports key space.
        var vertical = result.FindIndex(x => x.Id == 8);
        result.Insert(vertical < 0 ? result.Count : vertical + 1,
            new ProductionReportCatalogItem(TrapezoidEndpoints.ReportId,
                "shopfloor-trapezoid", "Vertikalios · Trapecija"));
        return result;
    }

    private static async Task<ProductionReportData?> ReadRowsAsync(SqlConnection connection,
        int taskId, int reportId, CancellationToken ct)
    {
        if (!Reports.TryGetValue(reportId, out var report)) return null;
        // The legacy report procedures do not enforce Loc_leidimas. Serializable
        // keeps the indexed GU composition stable from the ACL check through the
        // procedure result; a concurrent Access INSERT must wait for this read.
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
            IsolationLevel.Serializable, ct);
        await using var guard = new SqlCommand($"""
            SELECT d.iProductionOutputDocumentID
            FROM dbo.TAB_ProductionOutputDocuments d
            WHERE d.iProductionOutputDocumentID=@taskId
              AND d.sProductionOutputDocumentNo LIKE N'GU%'
              AND {ProductionTaskReader.Visibility}
              AND EXISTS(SELECT 1 FROM dbo.DIC_ProjectReports catalog
                WHERE catalog.iReportID=@reportId AND catalog.sCategory=N'gam'
                  AND catalog.sName=@reportName);
            """, connection, transaction) { CommandTimeout = 30 };
        guard.Parameters.Add("@taskId", SqlDbType.Int).Value = taskId;
        guard.Parameters.Add("@reportId", SqlDbType.Int).Value = reportId;
        guard.Parameters.Add("@reportName", SqlDbType.NVarChar, 128).Value = report.Name;
        if (await guard.ExecuteScalarAsync(ct) is null) return null;

        await using var command = new SqlCommand(report.Procedure, connection, transaction)
        { CommandType = CommandType.StoredProcedure, CommandTimeout = 30 };
        command.Parameters.Add("@TaskId", SqlDbType.Int).Value = taskId;
        var columns = new List<string>();
        var values = new List<object?[]>();
        await using (var rows = await command.ExecuteReaderAsync(ct))
        {
            for (var i = 0; i < rows.FieldCount; i++) columns.Add(rows.GetName(i));
            while (await rows.ReadAsync(ct))
            {
                if (values.Count == 5000)
                    throw new ProductionQueueAccessException("REPORT_TOO_LARGE",
                        "The report dataset exceeds the 5000-row preview limit.", 413);
                var row = new object?[rows.FieldCount];
                for (var i = 0; i < row.Length; i++)
                    row[i] = rows.IsDBNull(i) ? null : rows.GetValue(i);
                values.Add(row);
            }
        }
        // Twelve Access report definitions embed rsubParduotiPriedai. Fetch its
        // linked view under the same mapped principal, GU visibility guard and
        // serializable snapshot as the main report procedure.
        var accessories = report.HasAccessorySubreport && values.Count > 0
            ? await ReadAccessoriesAsync(connection, transaction, taskId, ct)
            : [];
        await transaction.CommitAsync(ct);
        return new ProductionReportData(taskId, reportId, report.Name, columns,
            values, values.Count == 0, accessories);
    }

    private static async Task<IReadOnlyList<ProductionReportAccessory>> ReadAccessoriesAsync(
        SqlConnection connection, SqlTransaction transaction, int taskId, CancellationToken ct)
    {
        await using var command = new SqlCommand("""
            SELECT a.ParduotoGaminioID, a.Quantity, a.Unit, a.AccessoryName
            FROM dbo.V_ParduotiPriedai a
            WHERE EXISTS (
                SELECT 1 FROM dbo.TAB_ProductionOutputDocumentDetails d
                JOIN dbo.ParduotiGaminiai p ON p.iBarCode=d.iBarCode
                WHERE d.iProductionOutputDocumentID=@taskId
                  AND p.ParduotoGaminioID=a.ParduotoGaminioID)
            ORDER BY a.ParduotoGaminioID, a.AccessoryName;
            """, connection, transaction) { CommandTimeout = 30 };
        command.Parameters.Add("@taskId", SqlDbType.Int).Value = taskId;
        var result = new List<ProductionReportAccessory>();
        await using var rows = await command.ExecuteReaderAsync(ct);
        while (await rows.ReadAsync(ct))
        {
            if (result.Count == 5000)
                throw new ProductionQueueAccessException("REPORT_TOO_LARGE",
                    "The accessory dataset exceeds the 5000-row preview limit.", 413);
            result.Add(new ProductionReportAccessory(
                Convert.ToInt64(rows.GetValue(0)),
                rows.IsDBNull(1) ? 0 : Convert.ToDecimal(rows.GetValue(1)),
                rows.IsDBNull(2) ? null : Convert.ToString(rows.GetValue(2)),
                rows.IsDBNull(3) ? null : Convert.ToString(rows.GetValue(3))));
        }
        return result;
    }
}
