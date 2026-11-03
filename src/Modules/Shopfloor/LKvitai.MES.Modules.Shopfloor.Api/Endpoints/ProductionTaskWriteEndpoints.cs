using System.Data;
using Microsoft.Data.SqlClient;

namespace LKvitai.MES.Modules.Shopfloor.Api.Endpoints;

public sealed record IssueProductionPortionRequest(int? TaskId, long[] ProductIds);
public sealed record IssueProductionPortionResult(int TaskId, string Number, int IssuedRows);

/// <summary>Access NewTask/Gaminti: one GU header and one INSERT per barcode.</summary>
public static class ProductionTaskWriteEndpoints
{
    public static RouteGroupBuilder MapProductionTaskWriteEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/production-tasks/issue", async (IssueProductionPortionRequest request,
            ProductionTaskWriter writer, CancellationToken ct) =>
        {
            if (request.ProductIds is not { Length: > 0 and <= 100 }
                || request.ProductIds.Any(id => id <= 0)
                || request.ProductIds.Distinct().Count() != request.ProductIds.Length
                || request.TaskId is <= 0)
                return Results.BadRequest(new { code = "INVALID_PRODUCTION_PORTION" });
            try { return Results.Ok(await writer.IssueAsync(request, ct)); }
            catch (ProductionQueueAccessException ex) { return ProductionReadErrors.Access(ex); }
            catch (SqlException ex) when (ex.Number is 229 or 230) { return ProductionReadErrors.PermissionDenied(); }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            { return Results.Conflict(new { code = "PRODUCTION_PORTION_CONFLICT", message = "A selected item was already issued. Refresh the queue." }); }
        });
        return group;
    }
}

public sealed class ProductionTaskWriter(ProductionQueueReader queue)
{
    public Task<IssueProductionPortionResult> IssueAsync(IssueProductionPortionRequest request, CancellationToken ct)
        => queue.WithActorConnectionAsync((connection, token) => IssueRowsAsync(connection, request, token),
            ct, ProductionQueueReader.ProductionTaskMenuId);

    private static async Task<IssueProductionPortionResult> IssueRowsAsync(SqlConnection connection,
        IssueProductionPortionRequest request, CancellationToken ct)
    {
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            int taskId;
            string number;
            if (request.TaskId is int existing)
            {
                await using var task = new SqlCommand($"""
                    SELECT sProductionOutputDocumentNo
                    FROM dbo.TAB_ProductionOutputDocuments d WITH (UPDLOCK,HOLDLOCK)
                    WHERE iProductionOutputDocumentID=@id AND iAssignedStatusID=1
                      AND sProductionOutputDocumentNo LIKE N'GU%'
                      AND sUserNameInsert=SYSTEM_USER
                      AND (NOT EXISTS (SELECT 1 FROM dbo.TAB_ProductionOutputDocumentDetails n
                                       WHERE n.iProductionOutputDocumentID=d.iProductionOutputDocumentID)
                           OR ({ProductionTaskReader.Visibility}));
                    """, connection, transaction);
                task.Parameters.Add("@id", SqlDbType.Int).Value = existing;
                number = await task.ExecuteScalarAsync(ct) as string
                    ?? throw Denied("PRODUCTION_TASK_NOT_AVAILABLE", "This GU is not available to continue.", 404);
                taskId = existing;
            }
            else
            {
                // Access formats GU + YYMMDD + DCount(prefix) + 1. The table lock
                // serializes web requests; there is one production operator today.
                await using var create = new SqlCommand("""
                    DECLARE @now datetime=GETDATE();
                    DECLARE @prefix char(8)=N'GU'+RIGHT(CONVERT(char(4),YEAR(@now)),2)
                        +RIGHT(N'0'+CONVERT(varchar(2),MONTH(@now)),2)
                        +RIGHT(N'0'+CONVERT(varchar(2),DAY(@now)),2);
                    DECLARE @next int;
                    SELECT @next=COUNT(*)+1 FROM dbo.TAB_ProductionOutputDocuments WITH (TABLOCKX,HOLDLOCK)
                    WHERE LEFT(sProductionOutputDocumentNo,8)=@prefix;
                    DECLARE @number nvarchar(32)=@prefix+RIGHT(N'0'+CONVERT(nvarchar(10),@next),
                        CASE WHEN @next<100 THEN 2 ELSE LEN(CONVERT(nvarchar(10),@next)) END);
                    INSERT dbo.TAB_ProductionOutputDocuments
                        (sProductionOutputDocumentNo,dtDateTime,iAssignedStatusID)
                    VALUES (@number,@now,1);
                    SELECT CONVERT(int,SCOPE_IDENTITY()),@number;
                    """, connection, transaction);
                await using var created = await create.ExecuteReaderAsync(ct);
                if (!await created.ReadAsync(ct)) throw new DataException("GU header was not returned.");
                taskId = created.GetInt32(0);
                number = created.GetString(1);
            }

            // The same filtered source powers the live queue. Re-evaluate every
            // selected row inside the write transaction because Access may have
            // changed its state since the operator selected it.
            foreach (var productId in request.ProductIds)
            {
                await using var lockProduct = new SqlCommand("""
                    SELECT ParduotoGaminioID FROM dbo.ParduotiGaminiai WITH (UPDLOCK,HOLDLOCK)
                    WHERE ParduotoGaminioID=@productId;
                    """, connection, transaction);
                lockProduct.Parameters.Add("@productId", SqlDbType.BigInt).Value = productId;
                if (await lockProduct.ExecuteScalarAsync(ct) is null)
                    throw Denied("PRODUCTION_ITEM_NOT_AVAILABLE", "A selected item is no longer available. Refresh the queue.", 409);

                await using var eligible = new SqlCommand($"""
                    SELECT pg.iBarCode {ProductionQueueReader.QueueSourceSql}
                      AND pg.ParduotoGaminioID=@productId
                      AND UPPER(f.FilialoPavadinimas)=N'TESTAVIMAS';
                    """, connection, transaction);
                eligible.Parameters.Add("@productId", SqlDbType.BigInt).Value = productId;
                var barcode = await eligible.ExecuteScalarAsync(ct) as string;
                if (string.IsNullOrWhiteSpace(barcode))
                    throw Denied("PRODUCTION_ITEM_NOT_AVAILABLE", "A selected item is no longer available in Testavimas. Refresh the queue.", 409);

                // The legacy trigger reads scalar values from INSERTED; do not
                // combine multiple barcodes into a single INSERT statement.
                await using var insert = new SqlCommand("""
                    INSERT dbo.TAB_ProductionOutputDocumentDetails
                        (iProductionOutputDocumentID,iBarCode) VALUES (@taskId,@barcode);
                    """, connection, transaction);
                insert.Parameters.Add("@taskId", SqlDbType.Int).Value = taskId;
                insert.Parameters.Add("@barcode", SqlDbType.NVarChar, 50).Value = barcode;
                await insert.ExecuteNonQueryAsync(ct);
            }
            await transaction.CommitAsync(ct);
            return new IssueProductionPortionResult(taskId, number, request.ProductIds.Length);
        }
        catch
        {
            // Legacy triggers may issue ROLLBACK themselves. Preserve the
            // original SQL error if the transaction has already ended.
            try { await transaction.RollbackAsync(CancellationToken.None); }
            catch (InvalidOperationException) { }
            catch (SqlException) { }
            throw;
        }
    }

    private static ProductionQueueAccessException Denied(string code, string message, int status)
        => new(code, message, status);
}
