using System.Data;
using System.Security.Claims;
using System.Text.Json;
using LKvitai.MES.Modules.Shopfloor.Application.Trapezoid;
using LKvitai.MES.Modules.Shopfloor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace LKvitai.MES.Modules.Shopfloor.Api.Endpoints;

public sealed record TrapezoidSaveRequest(int TaskId, long ProductId, Guid RequestId,
    string ExpectedRevision, TrapezoidCalculateRequest Inputs,
    IReadOnlyDictionary<string, string>? Sources, string Language);
public sealed record TrapezoidVersionSnapshot(int TaskId, string TaskNumber,
    TrapezoidItem Item, int Version, TrapezoidCalculateRequest Inputs,
    IReadOnlyDictionary<string, string> Sources, TrapezoidCalculationResponse Calculation,
    DateTimeOffset SavedAt, string SavedBy, string Language);
public sealed record TrapezoidVersionSummary(int Version, DateTimeOffset SavedAt,
    string SavedBy, bool HasPdf, IReadOnlyDictionary<string, string> Sources);

public static class TrapezoidVersionEndpoints
{
    public static RouteGroupBuilder MapTrapezoidVersionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/production-tasks/{taskId:int}/trapezoid-items/{productId:long}/versions",
            async (int taskId, long productId, TrapezoidItemReader reader,
                TrapezoidVersionStore store, CancellationToken ct) =>
            {
                try
                {
                    if (!await VisibleAsync(reader, taskId, productId, ct)) return Results.NotFound();
                    return Results.Ok(await store.ListAsync(taskId, productId, ct));
                }
                catch (ProductionQueueAccessException ex) { return ProductionReadErrors.Access(ex); }
            });
        group.MapGet("/production-tasks/{taskId:int}/trapezoid-items/{productId:long}/versions/{version:int}",
            async (int taskId, long productId, int version, TrapezoidItemReader reader,
                TrapezoidVersionStore store, CancellationToken ct) =>
            {
                try
                {
                    if (!await VisibleAsync(reader, taskId, productId, ct)) return Results.NotFound();
                    var snapshot = await store.ReadAsync(taskId, productId, version, ct);
                    return snapshot is null ? Results.NotFound() : Results.Ok(snapshot);
                }
                catch (ProductionQueueAccessException ex) { return ProductionReadErrors.Access(ex); }
            });
        group.MapPost("/production-tasks/{taskId:int}/trapezoid-items/{productId:long}/versions/{version:int}/pdf",
            async (int taskId, long productId, int version, TrapezoidItemReader reader,
                TrapezoidVersionStore store, CancellationToken ct) =>
            {
                try
                {
                    if (!await VisibleAsync(reader, taskId, productId, ct)) return Results.NotFound();
                    var snapshot = await store.ReadAsync(taskId, productId, version, ct);
                    if (snapshot is null) return Results.NotFound();
                    var pdf = await store.GetPdfAsync(taskId, productId, version, ct);
                    if (pdf is null)
                    {
                        pdf = TrapezoidPdfRenderer.Render(snapshot);
                        await store.SavePdfAsync(taskId, productId, version, pdf, ct);
                    }
                    return Results.Ok(new { version, bytes = pdf.Length });
                }
                catch (ProductionQueueAccessException ex) { return ProductionReadErrors.Access(ex); }
            });
        group.MapGet("/production-tasks/{taskId:int}/trapezoid-items/{productId:long}/versions/{version:int}/pdf",
            async (int taskId, long productId, int version, TrapezoidItemReader reader,
                TrapezoidVersionStore store, HttpContext http, CancellationToken ct) =>
            {
                try
                {
                    if (!await VisibleAsync(reader, taskId, productId, ct)) return Results.NotFound();
                    var pdf = await store.GetPdfAsync(taskId, productId, version, ct);
                    if (pdf is null) return Results.NotFound();
                    http.Response.Headers.CacheControl = "no-store";
                    return Results.File(pdf, "application/pdf", enableRangeProcessing: true);
                }
                catch (ProductionQueueAccessException ex) { return ProductionReadErrors.Access(ex); }
            });
        group.MapPost("/trapezoid/versions", async (TrapezoidSaveRequest request,
            TrapezoidItemReader reader, ProductionQueueReader queue, TrapezoidVersionStore store,
            HttpContext http, CancellationToken ct) =>
        {
            var validInputs = TrapezoidEndpoints.TryInputs(request.Inputs, out var inputs, out var code);
            if (request.TaskId <= 0 || request.ProductId <= 0 || request.RequestId == Guid.Empty ||
                request.ExpectedRevision?.Length != 64 || request.Language is not ("en" or "lt") ||
                !validInputs)
                return Results.BadRequest(new { code = code ?? "INVALID_SAVE_REQUEST" });
            try
            {
                await queue.WithActorConnectionAsync((_, _) => Task.FromResult(true), ct,
                    ProductionQueueReader.ProductionTaskMenuId);
                var list = await reader.ReadAsync(request.TaskId, ct);
                var item = list?.Items.FirstOrDefault(i => i.ProductId == request.ProductId);
                if (item is null) return Results.NotFound();
                if (!item.Supported) return Results.BadRequest(new { code = "UNSUPPORTED_PRODUCT_TYPE" });
                if (!string.Equals(item.Revision, request.ExpectedRevision, StringComparison.Ordinal))
                    return Results.Conflict(new { code = "ORDER_CHANGED" });
                TrapezoidResult result;
                try { result = TrapezoidCalculator.Calculate(inputs!); }
                catch (TrapezoidCalculationException ex)
                { return Results.BadRequest(new { code = ex.Code, detail = ex.Message }); }
                var actor = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? http.User.Identity?.Name ?? "unknown";
                var snapshot = new TrapezoidVersionSnapshot(request.TaskId, list!.TaskNumber,
                    item, 0, request.Inputs,
                    VerifiedSources(item, request.Inputs),
                    new TrapezoidCalculationResponse(result, TrapezoidCalculator.ProfileVersion,
                        TrapezoidCalculator.LookupTableVersion, TrapezoidCalculator.AlgorithmVersion),
                    DateTimeOffset.UtcNow, actor, request.Language);
                return Results.Ok(await store.SaveAsync(snapshot, request.RequestId, ct));
            }
            catch (ProductionQueueAccessException ex) { return ProductionReadErrors.Access(ex); }
            catch (SqlException ex) when (ex.Number is 229 or 230) { return ProductionReadErrors.PermissionDenied(); }
        });
        return group;
    }

    internal static async Task<bool> VisibleAsync(TrapezoidItemReader reader, int taskId,
        long productId, CancellationToken ct) =>
        (await reader.ReadAsync(taskId, ct))?.Items.Any(i => i.ProductId == productId) == true;

    private static IReadOnlyDictionary<string, string> VerifiedSources(TrapezoidItem item,
        TrapezoidCalculateRequest inputs) => new Dictionary<string, string>
    {
        ["controlType"] = inputs.ControlType == item.ControlType ? "item" : "manual",
        ["slatWidthMm"] = inputs.SlatWidthMm == item.SlatWidthItemMm ? "item" : "manual",
        ["widthMm"] = Matches(inputs.WidthMm, item.WidthMm) ? "order" : "manual",
        ["tallHeightMm"] = Matches(inputs.TallHeightMm, item.TallHeightMm) ? "order" : "manual",
        ["shortHeightMm"] = Matches(inputs.ShortHeightMm, item.ShortHeightMm) ? "order" : "manual",
        ["overhangMm"] = Matches(inputs.OverhangMm, item.OverhangMm) ? "order" : "manual",
        ["highSide"] = "manual" // Legacy has no verified orientation field.
    };

    private static bool Matches(double? input, decimal? source) =>
        input.HasValue && source.HasValue &&
        Math.Abs(input.Value - (double)source.Value) < 0.000001;
}

public sealed class TrapezoidVersionStore(ShopfloorDbContext db)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<IReadOnlyList<TrapezoidVersionSummary>> ListAsync(int taskId,
        long productId, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = (NpgsqlConnection)db.Database.GetDbConnection();
            await using var command = new NpgsqlCommand("""
                SELECT version,created_at,created_by,pdf_bytes IS NOT NULL,snapshot_json
                FROM shopfloor.trapezoid_versions
                WHERE task_id=@task AND product_id=@product ORDER BY version DESC
                """, connection);
            command.Parameters.AddWithValue("task", taskId);
            command.Parameters.AddWithValue("product", productId);
            var result = new List<TrapezoidVersionSummary>();
            await using var rows = await command.ExecuteReaderAsync(ct);
            while (await rows.ReadAsync(ct))
            {
                var snapshot = JsonSerializer.Deserialize<TrapezoidVersionSnapshot>(rows.GetString(4), Json)!;
                result.Add(new(rows.GetInt32(0), rows.GetFieldValue<DateTimeOffset>(1),
                    rows.GetString(2), rows.GetBoolean(3), snapshot.Sources));
            }
            return result;
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }

    public async Task<TrapezoidVersionSnapshot?> ReadAsync(int taskId, long productId,
        int version, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = (NpgsqlConnection)db.Database.GetDbConnection();
            await using var command = new NpgsqlCommand("""
                SELECT snapshot_json FROM shopfloor.trapezoid_versions
                WHERE task_id=@task AND product_id=@product AND version=@version
                """, connection);
            command.Parameters.AddWithValue("task", taskId);
            command.Parameters.AddWithValue("product", productId);
            command.Parameters.AddWithValue("version", version);
            var json = await command.ExecuteScalarAsync(ct) as string;
            return json is null ? null : JsonSerializer.Deserialize<TrapezoidVersionSnapshot>(json, Json);
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }

    public async Task<TrapezoidVersionSnapshot> SaveAsync(TrapezoidVersionSnapshot snapshot,
        Guid requestId, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = (NpgsqlConnection)db.Database.GetDbConnection();
            await using var tx = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await using var lockCommand = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtextextended(@key,0))", connection, tx);
            lockCommand.Parameters.AddWithValue("key", $"trapezoid:{snapshot.TaskId}:{snapshot.Item.ProductId}");
            await lockCommand.ExecuteNonQueryAsync(ct);
            await using var existing = new NpgsqlCommand("""
                SELECT snapshot_json FROM shopfloor.trapezoid_versions
                WHERE task_id=@task AND product_id=@product AND request_id=@request
                """, connection, tx);
            existing.Parameters.AddWithValue("task", snapshot.TaskId);
            existing.Parameters.AddWithValue("product", snapshot.Item.ProductId);
            existing.Parameters.AddWithValue("request", requestId);
            var old = await existing.ExecuteScalarAsync(ct) as string;
            if (old is not null) return JsonSerializer.Deserialize<TrapezoidVersionSnapshot>(old, Json)!;
            await using var next = new NpgsqlCommand("""
                SELECT COALESCE(MAX(version),0)+1 FROM shopfloor.trapezoid_versions
                WHERE task_id=@task AND product_id=@product
                """, connection, tx);
            next.Parameters.AddWithValue("task", snapshot.TaskId);
            next.Parameters.AddWithValue("product", snapshot.Item.ProductId);
            var version = Convert.ToInt32(await next.ExecuteScalarAsync(ct));
            var saved = snapshot with { Version = version };
            await using var insert = new NpgsqlCommand("""
                INSERT INTO shopfloor.trapezoid_versions
                (task_id,product_id,version,request_id,source_revision,snapshot_json,created_at,created_by)
                VALUES (@task,@product,@version,@request,@revision,CAST(@snapshot AS jsonb),@created,@actor)
                """, connection, tx);
            insert.Parameters.AddWithValue("task", snapshot.TaskId);
            insert.Parameters.AddWithValue("product", snapshot.Item.ProductId);
            insert.Parameters.AddWithValue("version", version);
            insert.Parameters.AddWithValue("request", requestId);
            insert.Parameters.AddWithValue("revision", snapshot.Item.Revision);
            insert.Parameters.AddWithValue("snapshot", JsonSerializer.Serialize(saved, Json));
            insert.Parameters.AddWithValue("created", saved.SavedAt);
            insert.Parameters.AddWithValue("actor", saved.SavedBy);
            await insert.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return saved;
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }

    public async Task<byte[]?> GetPdfAsync(int taskId, long productId, int version, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = (NpgsqlConnection)db.Database.GetDbConnection();
            await using var command = new NpgsqlCommand("""
                SELECT pdf_bytes FROM shopfloor.trapezoid_versions
                WHERE task_id=@task AND product_id=@product AND version=@version
                """, connection);
            command.Parameters.AddWithValue("task", taskId);
            command.Parameters.AddWithValue("product", productId);
            command.Parameters.AddWithValue("version", version);
            return await command.ExecuteScalarAsync(ct) as byte[];
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }

    public async Task SavePdfAsync(int taskId, long productId, int version, byte[] pdf, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = (NpgsqlConnection)db.Database.GetDbConnection();
            await using var command = new NpgsqlCommand("""
                UPDATE shopfloor.trapezoid_versions SET pdf_bytes=@pdf
                WHERE task_id=@task AND product_id=@product AND version=@version AND pdf_bytes IS NULL
                """, connection);
            command.Parameters.AddWithValue("task", taskId);
            command.Parameters.AddWithValue("product", productId);
            command.Parameters.AddWithValue("version", version);
            command.Parameters.AddWithValue("pdf", pdf);
            await command.ExecuteNonQueryAsync(ct);
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}
