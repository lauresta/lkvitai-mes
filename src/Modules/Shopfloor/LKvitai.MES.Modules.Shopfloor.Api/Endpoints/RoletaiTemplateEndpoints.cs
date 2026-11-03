using System.Data;
using System.Security.Claims;
using System.Text.Json;
using LKvitai.MES.Modules.Shopfloor.Contracts.Reports;
using LKvitai.MES.Modules.Shopfloor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LKvitai.MES.Modules.Shopfloor.Api.Endpoints;

/// <summary>Own-PostgreSQL layout storage. Legacy SQL remains the read-only data source.</summary>
public static class RoletaiTemplateEndpoints
{
    public static RouteGroupBuilder MapRoletaiTemplateEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/production-reports/roletai/template", ReadAsync);
        group.MapPut("/production-reports/roletai/template/draft", SaveAsync);
        group.MapPost("/production-reports/roletai/template/publish", PublishAsync);
        return group;
    }

    private static async Task<IResult> ReadAsync(ProductionQueueReader queue,
        RoletaiTemplateStore store, HttpContext http, CancellationToken ct)
    {
        try
        {
            await CheckMenuAsync(queue, ct);
            return Results.Ok(await store.ReadAsync(CanEdit(http.User), ct));
        }
        catch (ProductionQueueAccessException ex) { return ProductionReadErrors.Access(ex); }
    }

    private static async Task<IResult> SaveAsync(RoletaiTemplate template,
        ProductionQueueReader queue, RoletaiTemplateStore store, HttpContext http, CancellationToken ct)
    {
        if (!CanEdit(http.User)) return Results.Forbid();
        var errors = template.Validate();
        if (errors.Count > 0) return Results.BadRequest(new { code = "INVALID_TEMPLATE", errors });
        try
        {
            await CheckMenuAsync(queue, ct);
            return Results.Ok(await store.SaveDraftAsync(template, ct));
        }
        catch (ProductionQueueAccessException ex) { return ProductionReadErrors.Access(ex); }
    }

    private static async Task<IResult> PublishAsync(RoletaiPublishRequest request,
        ProductionQueueReader queue, RoletaiTemplateStore store, HttpContext http, CancellationToken ct)
    {
        if (!CanEdit(http.User)) return Results.Forbid();
        try
        {
            await CheckMenuAsync(queue, ct);
            var state = await store.PublishAsync(request.ExpectedVersion,
                http.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? http.User.Identity?.Name ?? "unknown", ct);
            return state is null ? Results.Conflict(new { code = "TEMPLATE_VERSION_CHANGED" }) : Results.Ok(state);
        }
        catch (ProductionQueueAccessException ex) { return ProductionReadErrors.Access(ex); }
        catch (InvalidDataException ex) { return Results.BadRequest(new { code = "INVALID_TEMPLATE", message = ex.Message }); }
    }

    private static bool CanEdit(ClaimsPrincipal user) =>
        user.IsInRole("Admin") || user.IsInRole("PortalAdmin") ||
        user.IsInRole("ShopfloorSupervisor") || user.IsInRole("ShopfloorTechnologist");

    private static Task<bool> CheckMenuAsync(ProductionQueueReader queue, CancellationToken ct) =>
        queue.WithActorConnectionAsync((_, _) => Task.FromResult(true), ct,
            ProductionQueueReader.ProductionTaskMenuId, ProductionQueueReader.FinishedGoodsMenuId);
}

public sealed record RoletaiPublishRequest(int ExpectedVersion);

public sealed class RoletaiTemplateStore(ShopfloorDbContext db)
{
    private const string Report = "rptGamDokRoletai";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<RoletaiTemplateState> ReadAsync(bool canEdit, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = (NpgsqlConnection)db.Database.GetDbConnection();
            await using var command = new NpgsqlCommand("""
                SELECT draft_json,published_json,published_version FROM shopfloor.production_report_templates
                WHERE report_name=@report
                """, connection);
            command.Parameters.AddWithValue("report", Report);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
            {
                var defaultTemplate = RoletaiTemplate.Default();
                return new(defaultTemplate, defaultTemplate, 1, [], canEdit);
            }
            var draft = JsonSerializer.Deserialize<RoletaiTemplate>(reader.GetString(0), Json)!;
            var published = JsonSerializer.Deserialize<RoletaiTemplate>(reader.GetString(1), Json)!;
            var version = reader.GetInt32(2);
            await reader.CloseAsync();
            await using var versionsCommand = new NpgsqlCommand("""
                SELECT version,published_at,published_by FROM shopfloor.production_report_template_versions
                WHERE report_name=@report ORDER BY version DESC
                """, connection);
            versionsCommand.Parameters.AddWithValue("report", Report);
            var versions = new List<RoletaiTemplateVersion>();
            await using var rows = await versionsCommand.ExecuteReaderAsync(ct);
            while (await rows.ReadAsync(ct))
                versions.Add(new(rows.GetInt32(0), rows.GetFieldValue<DateTimeOffset>(1), rows.GetString(2)));
            return new(canEdit ? draft : published, published, version, versions, canEdit);
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }

    public async Task<RoletaiTemplateState> SaveDraftAsync(RoletaiTemplate template, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = (NpgsqlConnection)db.Database.GetDbConnection();
            await using var command = new NpgsqlCommand("""
                INSERT INTO shopfloor.production_report_templates
                    (report_name,draft_json,published_json,published_version)
                VALUES (@report,@draft,@initial,1)
                ON CONFLICT (report_name) DO UPDATE SET draft_json=EXCLUDED.draft_json,updated_at=now()
                """, connection);
            command.Parameters.AddWithValue("report", Report);
            command.Parameters.AddWithValue("draft", JsonSerializer.Serialize(template, Json));
            command.Parameters.AddWithValue("initial", JsonSerializer.Serialize(RoletaiTemplate.Default(), Json));
            await command.ExecuteNonQueryAsync(ct);
        }
        finally { await db.Database.CloseConnectionAsync(); }
        return await ReadAsync(true, ct);
    }

    public async Task<RoletaiTemplateState?> PublishAsync(int expectedVersion, string actor, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var connection = (NpgsqlConnection)db.Database.GetDbConnection();
            await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await using var select = new NpgsqlCommand("""
                SELECT draft_json,published_version FROM shopfloor.production_report_templates
                WHERE report_name=@report FOR UPDATE
                """, connection, transaction);
            select.Parameters.AddWithValue("report", Report);
            await using var reader = await select.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            var json = reader.GetString(0);
            var currentVersion = reader.GetInt32(1);
            await reader.CloseAsync();
            if (currentVersion != expectedVersion) return null;
            var template = JsonSerializer.Deserialize<RoletaiTemplate>(json, Json)
                ?? throw new InvalidDataException("Šablono duomenys negalioja.");
            var errors = template.Validate();
            if (errors.Count > 0) throw new InvalidDataException(string.Join(" ", errors));
            var next = currentVersion + 1;
            await using var update = new NpgsqlCommand("""
                UPDATE shopfloor.production_report_templates
                SET published_json=draft_json,published_version=@version,updated_at=now()
                WHERE report_name=@report;
                INSERT INTO shopfloor.production_report_template_versions
                    (report_name,version,template_json,published_at,published_by)
                VALUES (@report,@version,@json,now(),@actor);
                """, connection, transaction);
            update.Parameters.AddWithValue("report", Report);
            update.Parameters.AddWithValue("version", next);
            update.Parameters.AddWithValue("json", json);
            update.Parameters.AddWithValue("actor", actor);
            await update.ExecuteNonQueryAsync(ct);
            await transaction.CommitAsync(ct);
        }
        finally { await db.Database.CloseConnectionAsync(); }
        return await ReadAsync(true, ct);
    }
}
