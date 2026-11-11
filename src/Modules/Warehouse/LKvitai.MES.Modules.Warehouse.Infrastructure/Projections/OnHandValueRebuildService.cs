using System.Data;
using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.Application.Projections;
using Npgsql;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Projections;

public partial class ProjectionRebuildService
{
    private const string OnHandProduction = "public.on_hand_value";
    private const string OnHandShadow = "public.on_hand_value_shadow";

    public async Task<OnHandValuePreview> PreviewAsync(CancellationToken ct = default)
    {
        await using var connection = await OpenOnHandConnectionAsync(ct);
        await using var gate = await AcquireOnHandLockAsync(connection, ct);
        await CreateShadowTableAsync(connection, OnHandProduction, OnHandShadow, ct);
        await ReplayOnHandValueAsync(OnHandShadow, connection, ct);
        // The displayed rows and checksums must describe the same database snapshot.
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var productionChecksum = await OnHandChecksumAsync(connection, OnHandProduction, ct);
        var shadowChecksum = await OnHandChecksumAsync(connection, OnHandShadow, ct);
        var rows = await ReadOnHandChangesAsync(connection, ct);
        await transaction.CommitAsync(ct);
        return new(productionChecksum, shadowChecksum, rows);
    }

    public async Task<bool> ApplyAsync(OnHandValueApply request, ProjectionAuditActor actor, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Reason);
        await using var connection = await OpenOnHandConnectionAsync(ct);
        await using var gate = await AcquireOnHandLockAsync(connection, ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT to_regclass('public.on_hand_value_shadow') IS NOT NULL";
        if (!(bool)(await command.ExecuteScalarAsync(ct))!) return false;
        // Serialize with normal projection writers as well as other rebuild requests.
        command.CommandText = "LOCK TABLE public.on_hand_value, public.on_hand_value_shadow IN ACCESS EXCLUSIVE MODE";
        await command.ExecuteNonQueryAsync(ct);
        var productionChecksum = await OnHandChecksumAsync(connection, OnHandProduction, ct);
        var shadowChecksum = await OnHandChecksumAsync(connection, OnHandShadow, ct);
        if (productionChecksum != request.ProductionChecksum || shadowChecksum != request.ShadowChecksum) return false;
        var changes = await ReadOnHandChangesAsync(connection, ct);
        command.CommandText = "SELECT count(*) FROM public.on_hand_value_shadow";
        var rowCount = (long)(await command.ExecuteScalarAsync(ct))!;
        // LIKE INCLUDING ALL omits foreign keys; preserve the register's existing constraints.
        command.CommandText = """
            DO $block$ DECLARE fk record; BEGIN
                FOR fk IN SELECT conname, pg_get_constraintdef(oid) AS definition
                          FROM pg_constraint WHERE conrelid = 'public.on_hand_value'::regclass AND contype = 'f'
                LOOP
                    EXECUTE format('ALTER TABLE public.on_hand_value_shadow ADD CONSTRAINT %I %s', fk.conname, fk.definition);
                END LOOP;
            END $block$;
            """;
        await command.ExecuteNonQueryAsync(ct);
        // Swap and audit commit together. Failure rolls both back.
        command.CommandText = """
            ALTER TABLE public.on_hand_value RENAME TO on_hand_value_replaced;
            ALTER TABLE public.on_hand_value_shadow RENAME TO on_hand_value;
            DROP TABLE public.on_hand_value_replaced;
            INSERT INTO public.security_audit_logs
                ("UserId", "Action", "Resource", "ResourceId", "IpAddress", "UserAgent", "Timestamp", "LegalHold", "Details")
            VALUES (@user, 'ProjectionRebuildApplied', 'OnHandValue', 'OnHandValue', @ip, @agent, @now, false, @details);
            """;
        command.Parameters.AddWithValue("user", actor.UserId);
        command.Parameters.AddWithValue("ip", actor.IpAddress);
        command.Parameters.AddWithValue("agent", actor.UserAgent.Length > 1000 ? actor.UserAgent[..1000] : actor.UserAgent);
        command.Parameters.AddWithValue("now", DateTimeOffset.UtcNow);
        command.Parameters.AddWithValue("details", JsonSerializer.Serialize(new
        {
            reason = request.Reason.Trim(), rowCount, changedRowCount = changes.Count,
            productionChecksum, shadowChecksum
        }));
        await command.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    private Task<string> OnHandChecksumAsync(NpgsqlConnection connection, string table, CancellationToken ct)
        => ComputeFieldChecksumAsync(connection, table, "OnHandValue", GetFieldBasedChecksumSql("OnHandValue"), ct);

    private static async Task<List<OnHandValueChange>> ReadOnHandChangesAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COALESCE(s."ItemSku", p."ItemSku"),
                   CASE WHEN p."Id" IS NULL THEN 'Added' WHEN s."Id" IS NULL THEN 'Removed' ELSE 'Changed' END,
                   p."Qty", p."UnitCost", p."TotalValue", s."Qty", s."UnitCost", s."TotalValue"
            FROM public.on_hand_value p FULL JOIN public.on_hand_value_shadow s ON s."Id" = p."Id"
            WHERE to_jsonb(p) IS DISTINCT FROM to_jsonb(s)
            ORDER BY COALESCE(s."ItemSku", p."ItemSku")
            """;
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<OnHandValueChange>();
        while (await reader.ReadAsync(ct))
        {
            OnHandValueAmounts? Amounts(int offset) => reader.IsDBNull(offset) ? null
                : new(reader.GetDecimal(offset), reader.GetDecimal(offset + 1), reader.GetDecimal(offset + 2));
            rows.Add(new(reader.GetString(0), reader.GetString(1), Amounts(2), Amounts(5)));
        }
        return rows;
    }

    private async Task<NpgsqlConnection> OpenOnHandConnectionAsync(CancellationToken ct)
    {
        var connectionString = _warehouseDbConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            await using var session = _documentStore.QuerySession();
            connectionString = session.Connection!.ConnectionString;
        }
        var connection = new NpgsqlConnection(connectionString);
        try { await connection.OpenAsync(ct); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }

    private static async Task<OnHandAdvisoryLock> AcquireOnHandLockAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_advisory_lock(148702, 8)";
        await command.ExecuteNonQueryAsync(ct);
        return new(connection);
    }

    private sealed class OnHandAdvisoryLock(NpgsqlConnection connection) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT pg_advisory_unlock(148702, 8)";
            await command.ExecuteNonQueryAsync();
        }
    }
}
