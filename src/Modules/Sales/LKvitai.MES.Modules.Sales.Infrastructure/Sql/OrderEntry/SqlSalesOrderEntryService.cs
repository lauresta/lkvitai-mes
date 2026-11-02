using LKvitai.MES.Modules.Sales.Infrastructure.Sql.Security;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;

/// <summary>Legacy shared-database editor. Every mutation has a durable atomic receipt and branch scope.</summary>
public sealed partial class SqlSalesOrderEntryService(SalesSqlOptions sql, SalesOrderEntryOptions options, ILegacySqlConnectionFactory connections, ILogger<SqlSalesOrderEntryService>? logger = null) : ISalesOrderEntryService, ISalesOrderFinancialCommands
{
    public async Task<OrderEditorSnapshot?> GetAsync(long orderId, CancellationToken ct)
    {
        await using var lease = await connections.OpenAsync(ct);
        var connection = lease.Connection;
        await using var tx = await lease.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var snapshot = await ReadAsync(connection, tx, orderId, ct);
        await tx.CommitAsync(ct);
        return snapshot;
    }

    public async Task<CommandOutcome?> GetOutcomeAsync(Guid actorId, Guid commandId, CancellationToken ct)
    {
        if (actorId == Guid.Empty || commandId == Guid.Empty) throw new OrderEntryException("INVALID_COMMAND", "Command identity is required.", 400);
        await using var lease = await connections.OpenAsync(ct); var connection = lease.Connection;
        await using var tx = await lease.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var rows = await Rows(connection, tx, "SELECT * FROM mes_sales.CommandReceipt WHERE ActorId=@actor AND CommandId=@command", ct, ("@actor",actorId),("@command",commandId));
        if (rows.Count == 0) { await tx.CommitAsync(ct); return null; }
        var r = rows.Single();
        var current = await ReadAsync(connection, tx, L(r,"OrderId")!.Value, ct);
        RequireScope(current ?? throw new OrderEntryException("NOT_FOUND", "Order not found.",404));
        await tx.CommitAsync(ct);
        var op=S(r,"Operation")!;var related=r.ContainsKey("RelatedEntityId")?L(r,"RelatedEntityId"):null;
        return new CommandOutcome(commandId,op,L(r,"OrderId")!.Value,L(r,"ProductId"),op=="payment.post.v1"?related:null,op=="production.confirm.v1"&&related.HasValue?checked((int)related.Value):null,ReceiptWarnings(r));
    }

    private async Task<OrderEntryCommandResult> Execute<T>(Guid actor, Guid commandId, string operation,
        long? orderId, string? revision, T request, Func<SqlConnection,SqlTransaction,OrderEditorSnapshot?,CancellationToken,Task<(long OrderId,long? ProductId)>> mutate, CancellationToken ct, IReadOnlyList<string>? warnings = null)
    {
        // SQL1205 proves this transaction was the deadlock victim and rolled back.
        // All other transport/commit errors remain unknown outcomes for same-key recovery.
        for(var attempt=0;;attempt++)
        {
            try { return await ExecuteAttempt(actor,commandId,operation,orderId,revision,request,mutate,ct,warnings); }
            catch(SqlException ex) when(ex.Number==1205 && attempt==0)
            { if(warnings is List<string> mutable)mutable.Clear(); }
        }
    }

    private async Task<OrderEntryCommandResult> ExecuteAttempt<T>(Guid actor, Guid commandId, string operation,
        long? orderId, string? revision, T request, Func<SqlConnection,SqlTransaction,OrderEditorSnapshot?,CancellationToken,Task<(long OrderId,long? ProductId)>> mutate, CancellationToken ct, IReadOnlyList<string>? warnings = null)
    {
        if (!options.Enabled) throw new OrderEntryException("WRITES_DISABLED", "Sales saving is currently disabled.",403);
        if (actor == Guid.Empty || commandId == Guid.Empty) throw new OrderEntryException("INVALID_COMMAND", "An authenticated actor and command ID are required.",400);
        if (options.ExpectedDatabaseGuid == Guid.Empty || options.AllowedSeriesIds.Length == 0)
            throw new OrderEntryException("WRITE_CONFIGURATION", "Sales test-write scope is not configured.",503);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(operation + "\n" + JsonSerializer.Serialize(new { OrderId = orderId, Request = request })));
        await using var lease = await connections.OpenAsync(ct); var connection = lease.Connection;
        await using var tx = await lease.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        try
        {
            if (lease.DatabaseGuid != options.ExpectedDatabaseGuid || !string.Equals(lease.DatabaseName,"LKVITAIDBV1SQL",StringComparison.OrdinalIgnoreCase))
                throw new OrderEntryException("DATABASE_MISMATCH", "Sales saving is disabled for this database.",503);
            // Resolve the requested parent before checking command existence or
            // conflicts. A revoked/foreign order must always remain a 404.
            OrderEditorSnapshot? before=null;
            if(orderId.HasValue)
            {
                before=await ReadAsync(connection,tx,orderId.Value,ct) ?? throw new OrderEntryException("NOT_FOUND","Order not found.",404);
                RequireScope(before);
            }
            var receipts = await Rows(connection,tx,"SELECT * FROM mes_sales.CommandReceipt WITH(UPDLOCK,HOLDLOCK) WHERE ActorId=@actor AND CommandId=@command",ct,("@actor",actor),("@command",commandId));
            if (receipts.Count != 0)
            {
                var receipt=receipts.Single();
                var saved=before is not null && before.Header.Id==L(receipt,"OrderId") ? before
                    : await ReadAsync(connection,tx,L(receipt,"OrderId")!.Value,ct) ?? throw new OrderEntryException("NOT_FOUND","Order not found.",404);
                RequireScope(saved);
                if (S(receipt,"Operation")!=operation || !CryptographicOperations.FixedTimeEquals((byte[])receipt["RequestHash"]!,hash))
                    throw new OrderEntryException("COMMAND_ID_REUSED", "This command ID was already used for different input.",409);
                var savedWarnings=ReceiptWarnings(receipt);
                await tx.CommitAsync(ct);
                return ResultFor(operation,saved with { Warnings = savedWarnings },IsFinancial(operation)&&receipt.ContainsKey("RelatedEntityId")?L(receipt,"RelatedEntityId"):L(receipt,"ProductId"),true);
            }
            if (before is not null && (string.IsNullOrWhiteSpace(revision) || before.Revision!=revision))
                throw new OrderEntryException("STALE_ORDER","Order changed in another session. Reload before saving.",409);
            var result = await mutate(connection,tx,before,ct);
            var current = await ReadAsync(connection,tx,result.OrderId,ct) ?? throw new DataException("Saved order could not be read.");
            RequireScope(current);
            await NonQuery(connection,tx,"INSERT mes_sales.CommandReceipt(ActorId,CommandId,Operation,RequestHash,OrderId,ProductId,SeriesId,ResultRevision,CommittedAtUtc,SqlLogin,BuildVersion,WarningsJson) VALUES(@actor,@command,@operation,@hash,@order,@product,@series,@revision,SYSUTCDATETIME(),SYSTEM_USER,@build,@warnings)",ct,
                ("@actor",actor),("@command",commandId),("@operation",operation),("@hash",hash),("@order",result.OrderId),("@product",IsFinancial(operation)?null:result.ProductId),("@series",current.Header.SeriesId),("@revision",current.Revision),("@build",options.BuildVersion.Length>64?options.BuildVersion[..64]:options.BuildVersion),("@warnings",warnings is { Count: > 0 }?JsonSerializer.Serialize(warnings):null));
            if(IsFinancial(operation))
                await NonQuery(connection,tx,"UPDATE mes_sales.CommandReceipt SET RelatedEntityId=@related WHERE ActorId=@actor AND CommandId=@command",ct,("@related",result.ProductId),("@actor",actor),("@command",commandId));
            await tx.CommitAsync(ct);
            return ResultFor(operation,current with { Warnings = warnings ?? current.Warnings },result.ProductId,false);
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        { await Rollback(tx); throw new OrderEntryException("DUPLICATE","A record with these identifiers already exists.",409); }
        catch { await Rollback(tx); throw; }
    }
    private static IReadOnlyList<string> ReceiptWarnings(IReadOnlyDictionary<string,object?> receipt)
    {
        if(!receipt.TryGetValue("WarningsJson",out var value)||value is null)return [];
        return JsonSerializer.Deserialize<string[]>((string)value)??[];
    }
    private static bool IsFinancial(string operation)=>operation is "payment.post.v1" or "production.confirm.v1";
    private static OrderEntryCommandResult ResultFor(string operation,OrderEditorSnapshot order,long? entityId,bool replayed)
        => new(order,IsFinancial(operation)?null:entityId,replayed,operation=="payment.post.v1"?entityId:null,
            operation=="production.confirm.v1"&&entityId.HasValue?checked((int)entityId.Value):null);
    private static async Task Rollback(SqlTransaction tx) { try { await tx.RollbackAsync(CancellationToken.None); } catch (InvalidOperationException) { } catch(SqlException) { } }
    private void RequireScope(OrderEditorSnapshot snapshot)
    {
        if (snapshot.Header.BranchId!=7 || !options.AllowedSeriesIds.Contains(snapshot.Header.SeriesId))
            throw new OrderEntryException("TEST_BRANCH_ONLY","Saving is restricted to the configured Testavimas series.",403);
    }
    private bool Scope(int branch,int series) => options.Enabled && branch==7 && options.AllowedSeriesIds.Contains(series);
    private static void RequireEditable(OrderEditorSnapshot snapshot)
    { if (!snapshot.Header.CanEdit || snapshot.Header.Cancelled) throw new OrderEntryException("ORDER_LOCKED","This order cannot be edited in its current state.",409); }
    private static ProductSnapshot Product(OrderEditorSnapshot snapshot,long productId)
    { var p=snapshot.Products.SingleOrDefault(x=>x.Id==productId) ?? throw new OrderEntryException("NOT_FOUND","Product does not belong to this order.",404); if(snapshot.Header.Cancelled||!p.CanEdit)throw new OrderEntryException("PRODUCT_LOCKED","Product is locked in its current state.",409); return p; }

    private SqlCommand Command(SqlConnection c,SqlTransaction tx,string text,params (string Name,object? Value)[] values)
    {
        var cmd=new SqlCommand(text,c,tx){CommandTimeout=sql.CommandTimeoutSeconds>0?sql.CommandTimeoutSeconds:30};
        foreach(var (name,value) in values)
        {
            var p=cmd.Parameters.AddWithValue(name,value??DBNull.Value);
            if(value is decimal){p.SqlDbType=SqlDbType.Decimal;p.Precision=28;p.Scale=10;}
        }
        return cmd;
    }
    private async Task<List<Dictionary<string,object?>>> Rows(SqlConnection c,SqlTransaction tx,string text,CancellationToken ct,params (string Name,object? Value)[] values)
    {
        var started=Stopwatch.GetTimestamp();
        try
        {
        await using var cmd=Command(c,tx,text,values);await using var r=await cmd.ExecuteReaderAsync(ct);
        var rows=new List<Dictionary<string,object?>>();
        while(await r.ReadAsync(ct)) {var row=new Dictionary<string,object?>(StringComparer.Ordinal);for(var i=0;i<r.FieldCount;i++)row.Add(r.GetName(i),r.IsDBNull(i)?null:r.GetValue(i));rows.Add(row);}return rows;
        }
        catch(SqlException ex){LogSqlFailure(text,started,ex);throw;}
    }
    private async Task<object?> Scalar(SqlConnection c,SqlTransaction tx,string text,CancellationToken ct,params (string Name,object? Value)[] values)
    {var started=Stopwatch.GetTimestamp();try{await using var cmd=Command(c,tx,text,values);var v=await cmd.ExecuteScalarAsync(ct);return v is DBNull?null:v;}catch(SqlException ex){LogSqlFailure(text,started,ex);throw;}}
    private async Task<int> NonQuery(SqlConnection c,SqlTransaction tx,string text,CancellationToken ct,params (string Name,object? Value)[] values)
    {var started=Stopwatch.GetTimestamp();try{await using var cmd=Command(c,tx,text,values);return await cmd.ExecuteNonQueryAsync(ct);}catch(SqlException ex){LogSqlFailure(text,started,ex);throw;}}
    private void LogSqlFailure(string commandText,long started,SqlException exception)
    {
        // Fingerprint the parameterized statement; never log SQL, values, messages,
        // customer information or connection details, even for provider error zero.
        var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(commandText)))[..16];
        var errors=string.Join(";",exception.Errors.Cast<SqlError>().Select(e=>$"{e.Number}/{e.State}/{e.Class}"));
        var nativeCode=exception.InnerException is System.ComponentModel.Win32Exception native?native.NativeErrorCode:(int?)null;
        logger?.LogError("Sales SQL statement {StatementFingerprint} failed after {ElapsedMs} ms; errors {SqlErrors}; inner type {InnerType}; native code {NativeCode}",
            fingerprint,Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds),errors,exception.InnerException?.GetType().Name,nativeCode);
    }
    private static string? S(Dictionary<string,object?> r,string key)=>r[key] is null?null:Convert.ToString(r[key],CultureInfo.InvariantCulture);
    private static int? I(Dictionary<string,object?> r,string key)=>r[key] is null?null:Convert.ToInt32(r[key],CultureInfo.InvariantCulture);
    private static long? L(Dictionary<string,object?> r,string key)=>r[key] is null?null:Convert.ToInt64(r[key],CultureInfo.InvariantCulture);
    private static decimal? D(Dictionary<string,object?> r,string key)=>r[key] is null?null:Convert.ToDecimal(r[key],CultureInfo.InvariantCulture);
    private static bool B(Dictionary<string,object?> r,string key)=>r[key] is not null && Convert.ToBoolean(r[key],CultureInfo.InvariantCulture);
    private static DateTime? DT(Dictionary<string,object?> r,string key)=>r[key] is null?null:Convert.ToDateTime(r[key],CultureInfo.InvariantCulture);
    private static DateOnly? Date(Dictionary<string,object?> r,string key)=>DT(r,key) is DateTime date?DateOnly.FromDateTime(date):null;
}
