using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using LKvitai.MES.Modules.Shopfloor.Application.Trapezoid;
using Microsoft.Data.SqlClient;

namespace LKvitai.MES.Modules.Shopfloor.Api.Endpoints;

public sealed record TrapezoidItem(long ProductId, string Barcode, string OrderNumber,
    int Position, int TypeId, string ProductType, string? MaterialCode, string? MaterialName,
    decimal Quantity, string? Control, string? ControlType, int? SlatWidthItemMm,
    decimal? WidthMm, decimal? TallHeightMm, decimal? ShortHeightMm,
    decimal? OverhangMm, string Revision, bool Supported);
public sealed record TrapezoidItemList(int TaskId, string TaskNumber, bool CanEdit,
    IReadOnlyList<TrapezoidItem> Items);
public sealed record TrapezoidCalculateRequest(string? ControlType, int? SlatWidthMm,
    double? WidthMm, double? ShortHeightMm, double? TallHeightMm, double? OverhangMm,
    string? HighSide);
public sealed record TrapezoidCalculationResponse(TrapezoidResult Result, string ProfileVersion,
    string TableVersion, string AlgorithmVersion);

public static class TrapezoidEndpoints
{
    public const int ReportId = 100001;
    public static RouteGroupBuilder MapTrapezoidEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/production-tasks/{taskId:int}/trapezoid-items", async (
            int taskId, TrapezoidItemReader reader, CancellationToken ct) =>
        {
            if (taskId < 1) return Results.BadRequest(new { code = "INVALID_TASK_ID" });
            try
            {
                var data = await reader.ReadAsync(taskId, ct);
                return data is null ? Results.NotFound() : Results.Ok(data);
            }
            catch (ProductionQueueAccessException ex) { return ProductionReadErrors.Access(ex); }
            catch (SqlException ex) when (ex.Number is 229 or 230) { return ProductionReadErrors.PermissionDenied(); }
        });
        group.MapPost("/trapezoid/calculate", (TrapezoidCalculateRequest request) =>
        {
            if (!TryInputs(request, out var inputs, out var code))
                return Results.BadRequest(new { code });
            try
            {
                var result = TrapezoidCalculator.Calculate(inputs!);
                return Results.Ok(new TrapezoidCalculationResponse(result,
                    TrapezoidCalculator.ProfileVersion, TrapezoidCalculator.LookupTableVersion,
                    TrapezoidCalculator.AlgorithmVersion));
            }
            catch (TrapezoidCalculationException ex)
            {
                return Results.BadRequest(new { code = ex.Code, detail = ex.Message });
            }
        });
        return group;
    }

    internal static bool TryInputs(TrapezoidCalculateRequest? request,
        out TrapezoidInputs? inputs, out string? code)
    {
        inputs = null;
        code = null;
        if (request is null || request.WidthMm is null || request.ShortHeightMm is null ||
            request.TallHeightMm is null || request.SlatWidthMm is null ||
            request.ControlType is null || request.HighSide is not ("Left" or "Right") ||
            request.ControlType.Trim().ToUpperInvariant() is not ("A" or "B") ||
            request.OverhangMm is null)
        {
            code = "INCOMPLETE_DIMENSIONS";
            return false;
        }
        inputs = new TrapezoidInputs(request.ControlType.Trim().ToUpperInvariant(),
            request.WidthMm.Value, request.ShortHeightMm.Value,
            request.TallHeightMm.Value, request.SlatWidthMm.Value, request.OverhangMm.Value);
        return true;
    }
}

public sealed class TrapezoidItemReader(ProductionQueueReader queue)
{
    private static readonly HashSet<int> SupportedTypes = [1084710449, 1171363884];
    public Task<TrapezoidItemList?> ReadAsync(int taskId, CancellationToken ct)
        => queue.WithActorConnectionAsync((connection, token) => ReadRowsAsync(connection, taskId, token),
            ct, ProductionQueueReader.ProductionTaskMenuId,
            ProductionQueueReader.FinishedGoodsMenuId);

    private static async Task<TrapezoidItemList?> ReadRowsAsync(SqlConnection connection, int taskId,
        CancellationToken ct)
    {
        // The document must be entirely visible under Loc_leidimas. The serializable
        // snapshot prevents Access from changing its composition during this read.
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        await using var guard = new SqlCommand($"""
            SELECT d.sProductionOutputDocumentNo FROM dbo.TAB_ProductionOutputDocuments d
            WHERE d.iProductionOutputDocumentID=@taskId
              AND d.sProductionOutputDocumentNo LIKE N'GU%'
              AND {ProductionTaskReader.Visibility};
            """, connection, tx) { CommandTimeout = 30 };
        guard.Parameters.Add("@taskId", SqlDbType.Int).Value = taskId;
        var taskNumber = await guard.ExecuteScalarAsync(ct) as string;
        if (taskNumber is null) return null;
        await using var command = new SqlCommand("""
            SELECT pg.ParduotoGaminioID, n.iBarCode,
                s.UzsakymoSerijaPav+N'-'+CONVERT(nvarchar(15),u.UzsakymoNumeris)+N'-'+u.UzsakymoGrupe,
                (SELECT COUNT(*) FROM dbo.ParduotiGaminiai ordinal
                  WHERE ordinal.UzsakymoID=pg.UzsakymoID
                    AND ordinal.ParduotoGaminioID<=pg.ParduotoGaminioID),
                pg.ParduotoGaminioTipoID, t.TipoPavadinimas,
                c.sCodeInternal, c.sName, pg.Kiekis, pg.Valdymas,
                pg.PPP01, pg.Plotis, pg.Aukstis, pg.PPP02, pg.PPP03, pg.PPP04,
                pg.dtDateUpdate
            FROM dbo.TAB_ProductionOutputDocumentDetails n
            JOIN dbo.ParduotiGaminiai pg ON pg.iBarCode=n.iBarCode
            JOIN dbo.Uzsakymai u ON u.UzsakymasID=pg.UzsakymoID
            JOIN dbo.Zinynas_UzsakymoSerijos s ON s.UzsakymoSerijaID=u.UzsakymoSerija
            JOIN dbo.Zinynas_tipai t ON t.TipoID=pg.ParduotoGaminioTipoID
            LEFT JOIN dbo.TBD_Components c ON c.iComponentID=pg.Spalva
            WHERE n.iProductionOutputDocumentID=@taskId
              AND pg.ParduotoGaminioTipoID IN
                (1084710449,1171363884,2112842587,2112842588,2112842471,446840360)
              AND EXISTS(SELECT 1 FROM dbo.Loc_leidimas() acl WHERE acl.UKS=s.FilialoID)
            ORDER BY pg.ParduotoGaminioID;
            """, connection, tx) { CommandTimeout = 30 };
        command.Parameters.Add("@taskId", SqlDbType.Int).Value = taskId;
        var result = new List<TrapezoidItem>();
        await using var rows = await command.ExecuteReaderAsync(ct);
        while (await rows.ReadAsync(ct))
        {
            var raw = Enumerable.Range(0, rows.FieldCount)
                .Select(i => rows.IsDBNull(i) ? "" : Convert.ToString(rows.GetValue(i), CultureInfo.InvariantCulture) ?? "")
                .ToArray();
            var revision = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(string.Join('\u001f', raw))));
            var typeId = rows.GetInt32(4);
            var control = Text(rows, 9)?.Trim();
            var slat = PositiveInt(Text(rows, 10));
            var tall = PositiveDecimal(Text(rows, 13)) ?? PositiveDecimal(Text(rows, 12));
            result.Add(new TrapezoidItem(Convert.ToInt64(rows.GetValue(0)), rows.GetString(1),
                rows.GetString(2), Convert.ToInt32(rows.GetValue(3)), typeId, rows.GetString(5),
                Text(rows, 6), Text(rows, 7), Convert.ToDecimal(rows.GetValue(8)), control,
                control?.ToUpperInvariant() is "A" or "B" ? control.ToUpperInvariant() : null,
                slat is 89 or 127 ? slat : null,
                PositiveDecimal(Text(rows, 11)), tall,
                PositiveDecimal(Text(rows, 14)), NonNegativeDecimal(Text(rows, 15)),
                revision, SupportedTypes.Contains(typeId)));
        }
        await tx.CommitAsync(ct);
        await using var edit = new SqlCommand("""
            SELECT CASE WHEN SYSTEM_USER=N'sa' OR EXISTS (
                SELECT 1 FROM dbo.PROJECT_TBD_UserMenus
                WHERE sUserName=SYSTEM_USER AND iProjectMenuButtonID=@menu
                  AND bItemEnabledFlag=1) THEN 1 ELSE 0 END;
            """, connection);
        edit.Parameters.Add("@menu", SqlDbType.Int).Value = ProductionQueueReader.ProductionTaskMenuId;
        var canEdit = Convert.ToInt32(await edit.ExecuteScalarAsync(ct)) == 1;
        return new TrapezoidItemList(taskId, taskNumber, canEdit, result);
    }

    private static string? Text(SqlDataReader rows, int i) =>
        rows.IsDBNull(i) ? null : Convert.ToString(rows.GetValue(i), CultureInfo.InvariantCulture);
    private static decimal? PositiveDecimal(string? text) =>
        ParseDecimal(text) is { } value && value > 0 ? value : null;
    private static decimal? NonNegativeDecimal(string? text) =>
        ParseDecimal(text) is { } value && value >= 0 ? value : null;
    private static int? PositiveInt(string? text) =>
        ParseDecimal(text) is { } value && value > 0 && value <= int.MaxValue &&
        value == decimal.Truncate(value)
            ? (int)value : null;
    private static decimal? ParseDecimal(string? text) =>
        decimal.TryParse(text?.Trim().Replace(',', '.'), NumberStyles.Number,
            CultureInfo.InvariantCulture, out var value) ? value : null;
}
