using LKvitai.MES.Application.Commands;
using LKvitai.MES.Application.Projections;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.SharedKernel;
using Marten;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Diagnostics;
using System.Text.Json;

namespace LKvitai.MES.Infrastructure.Projections;

/// <summary>
/// Projection rebuild service implementation
/// [MITIGATION V-5] Implements deterministic projection rebuild with shadow table verification
/// 
/// Supported projections: LocationBalance, AvailableStock
/// 
/// Rebuild Contract per design document:
/// - Rule A: Global-sequence-ordered replay (by sequence number, not timestamp)
/// - Rule B: Self-contained event data (no external queries in Apply)
/// - Rule C: Rebuild verification gate (shadow table + field-based checksum + atomic swap)
///
/// [HOTFIX H / MED-01] Uses field-based checksum instead of jsonb::text to avoid
/// nondeterminism from JSON key ordering and whitespace differences.
/// </summary>
public class ProjectionRebuildService : IProjectionRebuildService
{
    private readonly IDocumentStore _documentStore;
    private readonly ILogger<ProjectionRebuildService> _logger;

    /// <summary>
    /// JSON serializer options matching Marten's default (camelCase property names).
    /// Shadow table data must be serialized identically to Marten's live data.
    /// </summary>
    private static readonly JsonSerializerOptions CamelCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ── Table name constants ────────────────────────────────────────────
    private const string LocationBalanceTable = "mt_doc_locationbalanceview";
    private const string AvailableStockTable = "mt_doc_availablestockview";

    public ProjectionRebuildService(
        IDocumentStore documentStore,
        ILogger<ProjectionRebuildService> logger)
    {
        _documentStore = documentStore;
        _logger = logger;
    }

    public async Task<Result<ProjectionRebuildReport>> RebuildProjectionAsync(
        string projectionName,
        bool verify = true,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Starting projection rebuild for {ProjectionName} with verify={Verify}",
            projectionName, verify);

        try
        {
            var tableName = projectionName switch
            {
                "LocationBalance" => LocationBalanceTable,
                "AvailableStock" => AvailableStockTable,
                _ => null
            };

            if (tableName is null)
            {
                return Result<ProjectionRebuildReport>.Fail(
                    DomainErrorCodes.InvalidProjectionName,
                    $"Projection '{projectionName}' rebuild not implemented");
            }

            var shadowTable = $"{tableName}_shadow";

            // Step 1: Create shadow table
            await CreateShadowTableAsync(tableName, shadowTable, cancellationToken);

            // Step 2: Replay events to shadow table in GLOBAL sequence order (V-5 Rule A)
            var eventsProcessed = projectionName switch
            {
                "LocationBalance" => await ReplayLocationBalanceEventsAsync(shadowTable, cancellationToken),
                "AvailableStock" => await ReplayAvailableStockEventsAsync(shadowTable, cancellationToken),
                _ => 0
            };

            // Step 3: Compute field-based checksums (MED-01 fix)
            var checksumSql = GetFieldBasedChecksumSql(projectionName);
            var productionChecksum = await ComputeFieldChecksumAsync(tableName, checksumSql, cancellationToken);
            var shadowChecksum = await ComputeFieldChecksumAsync(shadowTable, checksumSql, cancellationToken);

            var checksumMatch = productionChecksum == shadowChecksum;

            _logger.LogInformation(
                "Checksums - Production: {ProductionChecksum}, Shadow: {ShadowChecksum}, Match: {Match}",
                productionChecksum, shadowChecksum, checksumMatch);

            // Step 4: Verify and optionally swap
            var swapped = false;
            if (verify)
            {
                if (!checksumMatch)
                {
                    _logger.LogWarning(
                        "Checksum mismatch for projection {ProjectionName}. " +
                        "Production: {ProductionChecksum}, Shadow: {ShadowChecksum}. Swap aborted.",
                        projectionName, productionChecksum, shadowChecksum);

                    stopwatch.Stop();
                    return Result<ProjectionRebuildReport>.Fail(
                        DomainErrorCodes.ValidationError,
                        $"Checksum verification failed. " +
                        $"Production: {productionChecksum}, Shadow: {shadowChecksum}");
                }

                await SwapTablesAsync(tableName, shadowTable, cancellationToken);
                swapped = true;

                _logger.LogInformation(
                    "Projection {ProjectionName} rebuilt and swapped successfully",
                    projectionName);
            }
            else
            {
                _logger.LogInformation(
                    "Projection {ProjectionName} rebuilt to shadow table. Verify=false, no swap.",
                    projectionName);
            }

            stopwatch.Stop();

            var report = new ProjectionRebuildReport
            {
                ProjectionName = projectionName,
                EventsProcessed = eventsProcessed,
                ProductionChecksum = productionChecksum,
                ShadowChecksum = shadowChecksum,
                ChecksumMatch = checksumMatch,
                Swapped = swapped,
                Duration = stopwatch.Elapsed
            };

            return Result<ProjectionRebuildReport>.Ok(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rebuilding projection {ProjectionName}", projectionName);
            stopwatch.Stop();
            return Result<ProjectionRebuildReport>.Fail(
                DomainErrorCodes.InternalError,
                $"Rebuild failed: {ex.Message}");
        }
    }

    public async Task<ProjectionDiffReport> GenerateDiffReportAsync(
        string projectionName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating diff report for {ProjectionName}", projectionName);

        await Task.CompletedTask;
        return new ProjectionDiffReport
        {
            ProjectionName = projectionName,
            RowsOnlyInProduction = 0,
            RowsOnlyInShadow = 0,
            RowsWithDifferences = 0,
            SampleDifferences = new List<string> { "Diff report not yet implemented" }
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    // Shadow table management (shared)
    // ═══════════════════════════════════════════════════════════════════

    private async Task CreateShadowTableAsync(
        string productionTable, string shadowTable, CancellationToken ct)
    {
        _logger.LogInformation("Creating shadow table {ShadowTable}", shadowTable);

        await using var session = _documentStore.LightweightSession();
        var conn = (NpgsqlConnection)session.Connection!;

        await using var dropCmd = conn.CreateCommand();
        dropCmd.CommandText = $"DROP TABLE IF EXISTS {shadowTable} CASCADE";
        await dropCmd.ExecuteNonQueryAsync(ct);

        await using var createCmd = conn.CreateCommand();
        createCmd.CommandText = $"CREATE TABLE {shadowTable} (LIKE {productionTable} INCLUDING ALL)";
        await createCmd.ExecuteNonQueryAsync(ct);

        _logger.LogInformation("Shadow table {ShadowTable} created", shadowTable);
    }

    private async Task SwapTablesAsync(
        string productionTable, string shadowTable, CancellationToken ct)
    {
        _logger.LogInformation("Swapping {ShadowTable} to {ProductionTable}", shadowTable, productionTable);

        await using var session = _documentStore.LightweightSession();
        var conn = (NpgsqlConnection)session.Connection!;

        await using var transaction = await conn.BeginTransactionAsync(ct);

        try
        {
            var oldTable = $"{productionTable}_old";

            await using var renameOldCmd = conn.CreateCommand();
            renameOldCmd.Transaction = transaction;
            renameOldCmd.CommandText = $"ALTER TABLE {productionTable} RENAME TO {oldTable.Split('.').Last()}";
            await renameOldCmd.ExecuteNonQueryAsync(ct);

            await using var renameShadowCmd = conn.CreateCommand();
            renameShadowCmd.Transaction = transaction;
            renameShadowCmd.CommandText = $"ALTER TABLE {shadowTable} RENAME TO {productionTable.Split('.').Last()}";
            await renameShadowCmd.ExecuteNonQueryAsync(ct);

            await transaction.CommitAsync(ct);

            _logger.LogInformation("Tables swapped successfully");

            await using var dropCmd = conn.CreateCommand();
            dropCmd.CommandText = $"DROP TABLE IF EXISTS {oldTable} CASCADE";
            await dropCmd.ExecuteNonQueryAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Field-based checksum (MED-01 fix)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// [MED-01] Returns a SQL expression that extracts deterministic field values
    /// for checksum computation. Avoids jsonb::text nondeterminism from JSON key
    /// ordering and whitespace differences.
    /// </summary>
    private static string GetFieldBasedChecksumSql(string projectionName) => projectionName switch
    {
        "LocationBalance" =>
            "id || ':' || COALESCE(data->>'warehouseId','') || ':' || " +
            "COALESCE(data->>'location','') || ':' || " +
            "COALESCE(data->>'sku','') || ':' || " +
            "COALESCE(data->>'quantity','0')",

        "AvailableStock" =>
            "id || ':' || COALESCE(data->>'warehouseId','') || ':' || " +
            "COALESCE(data->>'location','') || ':' || " +
            "COALESCE(data->>'sku','') || ':' || " +
            "COALESCE(data->>'onHandQty','0') || ':' || " +
            "COALESCE(data->>'hardLockedQty','0') || ':' || " +
            "COALESCE(data->>'availableQty','0')",

        _ => "id || data::text"
    };

    private async Task<string> ComputeFieldChecksumAsync(
        string tableName, string fieldExpression, CancellationToken ct)
    {
        _logger.LogInformation("Computing field-based checksum for {TableName}", tableName);

        await using var session = _documentStore.QuerySession();
        var conn = (NpgsqlConnection)session.Connection!;

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            SELECT COALESCE(
                MD5(STRING_AGG({fieldExpression}, '|' ORDER BY id)),
                'empty'
            ) as checksum
            FROM {tableName}";

        var checksum = (string?)await cmd.ExecuteScalarAsync(ct) ?? "empty";

        _logger.LogInformation("Checksum for {TableName}: {Checksum}", tableName, checksum);
        return checksum;
    }

    // ═══════════════════════════════════════════════════════════════════
    // LocationBalance replay
    // ═══════════════════════════════════════════════════════════════════

    private async Task<int> ReplayLocationBalanceEventsAsync(
        string shadowTable, CancellationToken ct)
    {
        _logger.LogInformation("Replaying LocationBalance events in global sequence order (V-5 Rule A)");

        await using var session = _documentStore.QuerySession();

        // V-5 Rule A: ALL events ordered by global sequence (not timestamp, not per-stream)
        var allEvents = await session.Events
            .QueryAllRawEvents()
            .OrderBy(e => e.Sequence)
            .ToListAsync(ct);

        var balances = new Dictionary<string, LocationBalanceView>();
        int processed = 0;

        foreach (var rawEvent in allEvents)
        {
            if (rawEvent.Data is not StockMovedEvent evt) continue;
            processed++;

            // V-5 Rule B: Extract warehouseId from stream key (self-contained)
            var streamKey = rawEvent.StreamKey
                ?? throw new InvalidOperationException("StreamKey is null");
            var streamId = Domain.StockLedgerStreamId.Parse(streamKey);
            var warehouseId = streamId.WarehouseId;

            // FROM location (decrease)
            var fromKey = $"{warehouseId}:{evt.FromLocation}:{evt.SKU}";
            if (!balances.ContainsKey(fromKey))
            {
                balances[fromKey] = new LocationBalanceView
                {
                    Id = fromKey,
                    WarehouseId = warehouseId,
                    Location = evt.FromLocation,
                    SKU = evt.SKU,
                    Quantity = 0,
                    LastUpdated = evt.Timestamp
                };
            }
            balances[fromKey].Quantity -= evt.Quantity;
            balances[fromKey].LastUpdated = evt.Timestamp;

            // TO location (increase)
            var toKey = $"{warehouseId}:{evt.ToLocation}:{evt.SKU}";
            if (!balances.ContainsKey(toKey))
            {
                balances[toKey] = new LocationBalanceView
                {
                    Id = toKey,
                    WarehouseId = warehouseId,
                    Location = evt.ToLocation,
                    SKU = evt.SKU,
                    Quantity = 0,
                    LastUpdated = evt.Timestamp
                };
            }
            balances[toKey].Quantity += evt.Quantity;
            balances[toKey].LastUpdated = evt.Timestamp;
        }

        // Insert into shadow table
        await InsertDocumentsToShadowAsync(shadowTable, balances.Values, ct);

        _logger.LogInformation(
            "LocationBalance: replayed {EventCount} events → {RecordCount} records",
            processed, balances.Count);

        return processed;
    }

    // ═══════════════════════════════════════════════════════════════════
    // AvailableStock replay (HOTFIX H / CRIT-02)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Replays ALL relevant events in GLOBAL sequence order to rebuild AvailableStock.
    ///
    /// [V-5 Rule A] Events are ordered by global sequence number, NOT timestamp.
    ///              Events come from MULTIPLE stream types (StockLedger + Reservation).
    /// [V-5 Rule B] Apply logic uses only self-contained event data; no external queries.
    /// 
    /// Relevant event types:
    ///   - StockMovedEvent         → updates OnHandQty
    ///   - PickingStartedEvent     → increases HardLockedQty
    ///   - ReservationConsumedEvent → decreases HardLockedQty
    ///   - ReservationCancelledEvent → decreases HardLockedQty
    /// </summary>
    private async Task<int> ReplayAvailableStockEventsAsync(
        string shadowTable, CancellationToken ct)
    {
        _logger.LogInformation(
            "Replaying AvailableStock events in GLOBAL sequence order (V-5 Rule A)");

        await using var session = _documentStore.QuerySession();

        // V-5 Rule A: ALL events across ALL streams, ordered by global sequence.
        // We filter by type in C# after deserialization — safe and robust regardless
        // of Marten's internal event type naming convention.
        var allEvents = await session.Events
            .QueryAllRawEvents()
            .OrderBy(e => e.Sequence)
            .ToListAsync(ct);

        var views = new Dictionary<string, AvailableStockView>();
        int processed = 0;

        foreach (var rawEvent in allEvents)
        {
            switch (rawEvent.Data)
            {
                case StockMovedEvent stockMoved:
                    ApplyStockMovedToAvailableStock(rawEvent, stockMoved, views);
                    processed++;
                    break;

                case PickingStartedEvent pickingStarted:
                    ApplyPickingStartedToAvailableStock(pickingStarted, views);
                    processed++;
                    break;

                case ReservationConsumedEvent consumed:
                    ApplyReservationConsumedToAvailableStock(consumed, views);
                    processed++;
                    break;

                case ReservationCancelledEvent cancelled:
                    ApplyReservationCancelledToAvailableStock(cancelled, views);
                    processed++;
                    break;
            }
        }

        // Insert into shadow table
        await InsertDocumentsToShadowAsync(shadowTable, views.Values, ct);

        _logger.LogInformation(
            "AvailableStock: replayed {EventCount} events → {RecordCount} records",
            processed, views.Count);

        return processed;
    }

    // ── AvailableStock apply helpers (mirror AvailableStockProjection) ──

    private static void ApplyStockMovedToAvailableStock(
        Marten.Events.IEvent rawEvent,
        StockMovedEvent evt,
        Dictionary<string, AvailableStockView> views)
    {
        // Extract warehouseId from stock-ledger stream key
        var streamKey = rawEvent.StreamKey
            ?? throw new InvalidOperationException("StreamKey is null for StockMovedEvent");
        var streamId = Domain.StockLedgerStreamId.Parse(streamKey);
        var warehouseId = streamId.WarehouseId;

        // FROM location (decrease OnHandQty)
        var fromKey = AvailableStockView.ComputeId(warehouseId, evt.FromLocation, evt.SKU);
        var fromView = GetOrCreateView(views, fromKey, warehouseId, evt.FromLocation, evt.SKU);
        fromView.OnHandQty -= evt.Quantity;
        fromView.RecomputeAvailable();
        fromView.LastUpdated = evt.Timestamp;

        // TO location (increase OnHandQty)
        var toKey = AvailableStockView.ComputeId(warehouseId, evt.ToLocation, evt.SKU);
        var toView = GetOrCreateView(views, toKey, warehouseId, evt.ToLocation, evt.SKU);
        toView.OnHandQty += evt.Quantity;
        toView.RecomputeAvailable();
        toView.LastUpdated = evt.Timestamp;
    }

    private static void ApplyPickingStartedToAvailableStock(
        PickingStartedEvent evt,
        Dictionary<string, AvailableStockView> views)
    {
        foreach (var line in evt.HardLockedLines)
        {
            var key = AvailableStockView.ComputeId(line.WarehouseId, line.Location, line.SKU);
            var view = GetOrCreateView(views, key, line.WarehouseId, line.Location, line.SKU);
            view.HardLockedQty += line.HardLockedQty;
            view.RecomputeAvailable();
            view.LastUpdated = evt.Timestamp;
        }
    }

    private static void ApplyReservationConsumedToAvailableStock(
        ReservationConsumedEvent evt,
        Dictionary<string, AvailableStockView> views)
    {
        foreach (var line in evt.ReleasedHardLockLines)
        {
            var key = AvailableStockView.ComputeId(line.WarehouseId, line.Location, line.SKU);
            var view = GetOrCreateView(views, key, line.WarehouseId, line.Location, line.SKU);
            view.HardLockedQty = Math.Max(0m, view.HardLockedQty - line.HardLockedQty);
            view.RecomputeAvailable();
            view.LastUpdated = evt.Timestamp;
        }
    }

    private static void ApplyReservationCancelledToAvailableStock(
        ReservationCancelledEvent evt,
        Dictionary<string, AvailableStockView> views)
    {
        foreach (var line in evt.ReleasedHardLockLines)
        {
            var key = AvailableStockView.ComputeId(line.WarehouseId, line.Location, line.SKU);
            var view = GetOrCreateView(views, key, line.WarehouseId, line.Location, line.SKU);
            view.HardLockedQty = Math.Max(0m, view.HardLockedQty - line.HardLockedQty);
            view.RecomputeAvailable();
            view.LastUpdated = evt.Timestamp;
        }
    }

    private static AvailableStockView GetOrCreateView(
        Dictionary<string, AvailableStockView> views,
        string key, string warehouseId, string location, string sku)
    {
        if (!views.TryGetValue(key, out var view))
        {
            view = new AvailableStockView
            {
                Id = key,
                WarehouseId = warehouseId,
                Location = location,
                SKU = sku
            };
            views[key] = view;
        }
        return view;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Shadow table insertion (shared)
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Inserts documents into the shadow table using camelCase JSON serialization
    /// to match Marten's default serializer format.
    /// </summary>
    private async Task InsertDocumentsToShadowAsync<T>(
        string shadowTable, IEnumerable<T> documents, CancellationToken ct)
        where T : class
    {
        await using var insertSession = _documentStore.LightweightSession();
        var conn = (NpgsqlConnection)insertSession.Connection!;

        foreach (var doc in documents)
        {
            // Get the Id property via reflection (all view models have string Id)
            var idProp = typeof(T).GetProperty("Id");
            var id = idProp?.GetValue(doc)?.ToString() ?? string.Empty;

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                INSERT INTO {shadowTable} (id, data)
                VALUES (@id, @data::jsonb)";
            cmd.Parameters.AddWithValue("id", id);
            // [MED-01] Use camelCase serialization to match Marten's default serializer
            cmd.Parameters.AddWithValue("data", JsonSerializer.Serialize(doc, CamelCaseOptions));
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
