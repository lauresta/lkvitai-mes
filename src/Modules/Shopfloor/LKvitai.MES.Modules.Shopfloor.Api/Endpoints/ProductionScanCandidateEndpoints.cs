using System.Data;
using Microsoft.Data.SqlClient;

namespace LKvitai.MES.Modules.Shopfloor.Api.Endpoints;

/// <summary>Read-only Access manual-pick tree; no scan or state mutation.</summary>
public static class ProductionScanCandidateEndpoints
{
    public static RouteGroupBuilder MapProductionScanCandidateEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/production-scan-candidates", async (int? stateId, int? page,
            int? pageSize, ProductionScanCandidateReader reader, CancellationToken ct) =>
        {
            if (stateId is null or <= 0 || page is < 1 or > 100000 || pageSize is < 1 or > 100)
                return Results.BadRequest(new { code = "INVALID_SCAN_CANDIDATE_FILTER" });
            try { return Results.Ok(await reader.ListAsync(stateId.Value, page ?? 1, pageSize ?? 50, ct)); }
            catch (ProductionQueueAccessException ex) { return ProductionReadErrors.Access(ex); }
            catch (SqlException ex) when (ex.Number is 229 or 230) { return ProductionReadErrors.PermissionDenied(); }
        });
        return group;
    }
}

public sealed record ProductionScanCandidate(string Department, string Series, string OrderNumber,
    string Barcode, string Description);
public sealed record ProductionScanCandidatePage(int Page, int PageSize, bool HasMore,
    IReadOnlyList<ProductionScanCandidate> Items);

public sealed class ProductionScanCandidateReader(ProductionQueueReader queue)
{
    public Task<ProductionScanCandidatePage> ListAsync(int stateId, int page, int pageSize,
        CancellationToken ct) => queue.WithActorConnectionAsync(
        (connection, token) => ReadAsync(connection, stateId, page, pageSize, token),
        ct, ProductionQueueReader.FinishedGoodsMenuId);

    private static async Task<ProductionScanCandidatePage> ReadAsync(SqlConnection connection,
        int stateId, int page, int pageSize, CancellationToken ct)
    {
        // The Access dialog obtains this list under the current SQL login.
        // A caller cannot enumerate candidates for a state absent from that list.
        await using (var states = new SqlCommand("dbo.sp_cbo_AssignedStateID_dlgNewProductionOutputDocument",
            connection) { CommandType = CommandType.StoredProcedure, CommandTimeout = 30 })
        await using (var rows = await states.ExecuteReaderAsync(ct))
        {
            var allowed = false;
            while (await rows.ReadAsync(ct))
                allowed |= Convert.ToInt32(rows.GetValue(0)) == stateId;
            if (!allowed)
                throw new ProductionQueueAccessException("PRODUCTION_STATE_NOT_ALLOWED",
                    "Your legacy SQL account cannot use this production operation.", 403);
        }

        await using var command = new SqlCommand(CandidateSql, connection) { CommandTimeout = 30 };
        command.Parameters.Add("@state", SqlDbType.Int).Value = stateId;
        command.Parameters.Add("@offset", SqlDbType.Int).Value = (page - 1) * pageSize;
        command.Parameters.Add("@take", SqlDbType.Int).Value = pageSize + 1;
        var items = new List<ProductionScanCandidate>(pageSize + 1);
        await using var candidates = await command.ExecuteReaderAsync(ct);
        while (await candidates.ReadAsync(ct))
            items.Add(new ProductionScanCandidate(candidates.GetString(0), candidates.GetString(1),
                candidates.GetString(2), candidates.GetString(3), candidates.GetString(4)));
        var hasMore = items.Count > pageSize;
        if (hasMore) items.RemoveAt(pageSize);
        return new ProductionScanCandidatePage(page, pageSize, hasMore, items);
    }

    // The TVF already joins Loc_leidimas and applies RULES_StateToState exactly as
    // frmScanning.FillTreeView. Recheck branch access at the API boundary too.
    private const string CandidateSql = """
        SELECT c.sDepartmentname, c.sSeriesName, c.sOrderNo, c.sBarCode, c.sItemDescription
        FROM dbo.lnfn_ListReadayForScanningBlinds(@state) c
        JOIN dbo.ParduotiGaminiai pg ON pg.iBarCode=c.sBarCode
        JOIN dbo.Uzsakymai u ON u.UzsakymasID=pg.UzsakymoID
        JOIN dbo.Zinynas_UzsakymoSerijos s ON s.UzsakymoSerijaID=u.UzsakymoSerija
        WHERE EXISTS (SELECT 1 FROM dbo.Loc_leidimas() acl WHERE acl.UKS=s.FilialoID)
        ORDER BY c.sDepartmentname, c.sSeriesName, c.sOrderNo, c.sBarCode
        OFFSET @offset ROWS FETCH NEXT @take ROWS ONLY;
        """;
}
