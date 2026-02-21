using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Projections;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Locking;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Domain = LKvitai.MES.Modules.Warehouse.Domain;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Projections;

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
    private static readonly Meter RebuildMeter = new("LKvitai.MES.Modules.Warehouse.Projections.Rebuild");
    private static readonly Histogram<double> RebuildDurationSeconds =
        RebuildMeter.CreateHistogram<double>("projection.rebuild.duration.seconds");

    private readonly IDocumentStore _documentStore;
    private readonly ILogger<ProjectionRebuildService> _logger;
    private readonly IDistributedLock _distributedLock;
    private readonly string? _warehouseDbConnectionString;

    // ── Table name constants ────────────────────────────────────────────
    private const string LocationBalanceTable = "mt_doc_locationbalanceview";
    private const string AvailableStockTable = "mt_doc_availablestockview";
    private const string OutboundOrderSummaryTable = "outbound_order_summary";
    private const string ShipmentSummaryTable = "shipment_summary";
    private const string DispatchHistoryTable = "dispatch_history";
    private const string OnHandValueTable = "on_hand_value";
    private const string InboundShipmentSummaryTable = "mt_doc_inboundshipmentsummaryview";

    public ProjectionRebuildService(
        IDocumentStore documentStore,
        ILogger<ProjectionRebuildService> logger)
        : this(documentStore, logger, new NoOpDistributedLock(), null)
    {
    }

    public ProjectionRebuildService(
        IDocumentStore documentStore,
        ILogger<ProjectionRebuildService> logger,
        IDistributedLock distributedLock)
        : this(documentStore, logger, distributedLock, null)
    {
    }

    public ProjectionRebuildService(
        IDocumentStore documentStore,
        ILogger<ProjectionRebuildService> logger,
        IDistributedLock distributedLock,
        IConfiguration? configuration)
    {
        _documentStore = documentStore;
        _logger = logger;
        _distributedLock = distributedLock;
        _warehouseDbConnectionString = configuration?.GetConnectionString("WarehouseDb");
    }

    public async Task<Result<ProjectionRebuildReport>> RebuildProjectionAsync(
        string projectionName,
        bool verify = true,
        bool resetProgress = false,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var lockKey = ProjectionRebuildLockKey.For(projectionName);
        var holder = $"{Environment.MachineName}:{Guid.NewGuid():N}";
        bool lockAcquired = false;

        _logger.LogInformation(
            "Starting projection rebuild for {ProjectionName} with verify={Verify}, resetProgress={ResetProgress}",
            projectionName, verify, resetProgress);

        try
        {
            var lockResult = await _distributedLock.TryAcquireAsync(
                lockKey,
                holder,
                TimeSpan.FromMinutes(30),
                cancellationToken);
            lockAcquired = lockResult.Acquired;
            if (!lockAcquired)
            {
                var existing = lockResult.ExistingLock;
                var message = existing is null
                    ? "Projection rebuild is already in progress."
                    : $"Projection rebuild is already in progress by '{existing.Holder}' until {existing.ExpiresAtUtc:O}.";

                RebuildDurationSeconds.Record(
                    stopwatch.Elapsed.TotalSeconds,
                    new KeyValuePair<string, object?>("projection", projectionName));
                return Result<ProjectionRebuildReport>.Fail(
                    DomainErrorCodes.IdempotencyInProgress,
                    message);
            }

            // Use a dedicated SQL connection for rebuild DDL/checksum/swap operations.
            // Marten session connections may already have an active transaction, which
            // would break local BeginTransactionAsync calls with nested transaction errors.
            // Prefer configured connection string so credentials (password) are preserved.
            var connectionString = _warehouseDbConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                await using var bootstrapSession = _documentStore.QuerySession();
                var bootstrapConnection = (NpgsqlConnection)(bootstrapSession.Connection
                    ?? throw new InvalidOperationException("Marten query session connection is unavailable."));
                connectionString = bootstrapConnection.ConnectionString;
            }

            await using var writeConnection = new NpgsqlConnection(connectionString);
            await writeConnection.OpenAsync(cancellationToken);

            var tableName = projectionName switch
            {
                "LocationBalance" => LocationBalanceTable,
                "AvailableStock" => AvailableStockTable,
                "OutboundOrderSummary" => OutboundOrderSummaryTable,
                "ShipmentSummary" => ShipmentSummaryTable,
                "DispatchHistory" => DispatchHistoryTable,
                "OnHandValue" => OnHandValueTable,
                "InboundShipmentSummary" => InboundShipmentSummaryTable,
                _ => null
            };

            if (tableName is null)
            {
                return Result<ProjectionRebuildReport>.Fail(
                    DomainErrorCodes.InvalidProjectionName,
                    $"Projection '{projectionName}' rebuild not implemented");
            }

            var productionTable = await ResolveQualifiedTableNameAsync(writeConnection, tableName, cancellationToken);
            if (string.IsNullOrWhiteSpace(productionTable))
            {
                return Result<ProjectionRebuildReport>.Fail(
                    DomainErrorCodes.NotFound,
                    $"Projection table for '{projectionName}' was not found.");
            }

            var shadowTable = BuildShadowTableName(productionTable);

            // Step 1: Create shadow table
            await CreateShadowTableAsync(writeConnection, productionTable, shadowTable, cancellationToken);

            // Step 2: Replay events to shadow table in GLOBAL sequence order (V-5 Rule A)
            var eventsProcessed = projectionName switch
            {
                "LocationBalance" => await ReplayLocationBalanceEventsAsync(shadowTable, writeConnection, cancellationToken),
                "AvailableStock" => await ReplayAvailableStockEventsAsync(shadowTable, writeConnection, cancellationToken),
                "OutboundOrderSummary" => await ReplayOutboundOrderSummaryAsync(shadowTable, writeConnection, cancellationToken),
                "ShipmentSummary" => await ReplayShipmentSummaryAsync(shadowTable, writeConnection, cancellationToken),
                "DispatchHistory" => await ReplayDispatchHistoryAsync(shadowTable, writeConnection, cancellationToken),
                "OnHandValue" => await ReplayOnHandValueAsync(shadowTable, writeConnection, cancellationToken),
                "InboundShipmentSummary" => await ReplayInboundShipmentSummaryAsync(shadowTable, writeConnection, cancellationToken),
                _ => 0
            };

            // Step 3: Compute field-based checksums (MED-01 fix)
            var checksumSql = GetFieldBasedChecksumSql(projectionName);
            var productionChecksum = await ComputeFieldChecksumAsync(
                writeConnection,
                productionTable,
                projectionName,
                checksumSql,
                cancellationToken);
            var shadowChecksum = await ComputeFieldChecksumAsync(
                writeConnection,
                shadowTable,
                projectionName,
                checksumSql,
                cancellationToken);

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
                    RebuildDurationSeconds.Record(
                        stopwatch.Elapsed.TotalSeconds,
                        new KeyValuePair<string, object?>("projection", projectionName));
                    return Result<ProjectionRebuildReport>.Fail(
                        DomainErrorCodes.ValidationError,
                        $"Checksum verification failed. " +
                        $"Production: {productionChecksum}, Shadow: {shadowChecksum}");
                }

                await SwapTablesAsync(writeConnection, productionTable, shadowTable, cancellationToken);
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

            RebuildDurationSeconds.Record(
                report.Duration.TotalSeconds,
                new KeyValuePair<string, object?>("projection", projectionName));

            return Result<ProjectionRebuildReport>.Ok(report);
        }
        catch (PostgresException ex) when (IsRebuildConflict(ex))
        {
            _logger.LogWarning(
                ex,
                "Projection rebuild for {ProjectionName} failed due to rebuild conflict ({SqlState})",
                projectionName,
                ex.SqlState);
            stopwatch.Stop();
            RebuildDurationSeconds.Record(
                stopwatch.Elapsed.TotalSeconds,
                new KeyValuePair<string, object?>("projection", projectionName));
            return Result<ProjectionRebuildReport>.Fail(
                DomainErrorCodes.IdempotencyInProgress,
                "Projection rebuild conflict detected. Retry the operation.");
        }
        catch (Exception ex)
        {
            if (TryGetRebuildConflict(ex, out var postgresException))
            {
                _logger.LogWarning(
                    postgresException,
                    "Projection rebuild for {ProjectionName} failed due to wrapped rebuild conflict ({SqlState})",
                    projectionName,
                    postgresException.SqlState);
                stopwatch.Stop();
                RebuildDurationSeconds.Record(
                    stopwatch.Elapsed.TotalSeconds,
                    new KeyValuePair<string, object?>("projection", projectionName));
                return Result<ProjectionRebuildReport>.Fail(
                    DomainErrorCodes.IdempotencyInProgress,
                    "Projection rebuild conflict detected. Retry the operation.");
            }

            _logger.LogError(ex, "Error rebuilding projection {ProjectionName}", projectionName);
            stopwatch.Stop();
            RebuildDurationSeconds.Record(
                stopwatch.Elapsed.TotalSeconds,
                new KeyValuePair<string, object?>("projection", projectionName));
            return Result<ProjectionRebuildReport>.Fail(
                DomainErrorCodes.InternalError,
                $"Rebuild failed: [{ex.GetType().Name}] {ex.Message}");
        }
        finally
        {
            if (lockAcquired)
            {
                try
                {
                    await _distributedLock.ReleaseAsync(lockKey, holder, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to release projection rebuild distributed lock cleanly");
                }
            }
        }
    }

    private static bool IsRebuildConflict(PostgresException ex)
    {
        return ex.SqlState is PostgresErrorCodes.UndefinedTable
            or PostgresErrorCodes.LockNotAvailable
            or PostgresErrorCodes.DeadlockDetected
            or PostgresErrorCodes.DuplicateTable
            or PostgresErrorCodes.ObjectInUse;
    }

    private static bool TryGetRebuildConflict(Exception ex, out PostgresException postgresException)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is PostgresException pg && IsRebuildConflict(pg))
            {
                postgresException = pg;
                return true;
            }
        }

        postgresException = null!;
        return false;
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

    public async Task<ProjectionRebuildLockStatus?> GetRebuildStatusAsync(
        string projectionName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectionName))
        {
            return null;
        }

        var lockInfo = await _distributedLock.GetActiveLockAsync(
            ProjectionRebuildLockKey.For(projectionName),
            cancellationToken);

        if (lockInfo is null)
        {
            return new ProjectionRebuildLockStatus
            {
                ProjectionName = projectionName,
                Locked = false
            };
        }

        return new ProjectionRebuildLockStatus
        {
            ProjectionName = projectionName,
            Locked = true,
            Holder = lockInfo.Holder,
            AcquiredAtUtc = lockInfo.AcquiredAtUtc,
            ExpiresAtUtc = lockInfo.ExpiresAtUtc
        };
    }

    // ═══════════════════════════════════════════════════════════════════
    // Shadow table management (shared)
    // ═══════════════════════════════════════════════════════════════════

    private async Task CreateShadowTableAsync(
        NpgsqlConnection connection,
        string productionTable,
        string shadowTable,
        CancellationToken ct)
    {
        _logger.LogInformation("Creating shadow table {ShadowTable}", shadowTable);

        await using var transaction = await connection.BeginTransactionAsync(ct);

        await using var dropCmd = connection.CreateCommand();
        dropCmd.Transaction = transaction;
        dropCmd.CommandText = $"DROP TABLE IF EXISTS {shadowTable} CASCADE";
        await dropCmd.ExecuteNonQueryAsync(ct);

        await using var createCmd = connection.CreateCommand();
        createCmd.Transaction = transaction;
        createCmd.CommandText = $"CREATE TABLE {shadowTable} (LIKE {productionTable} INCLUDING ALL)";
        await createCmd.ExecuteNonQueryAsync(ct);

        await transaction.CommitAsync(ct);
        _logger.LogInformation("Shadow table {ShadowTable} created", shadowTable);
    }

    private async Task SwapTablesAsync(
        NpgsqlConnection connection,
        string productionTable,
        string shadowTable,
        CancellationToken ct)
    {
        _logger.LogInformation("Swapping {ShadowTable} to {ProductionTable}", shadowTable, productionTable);

        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            var productionName = GetUnqualifiedTableName(productionTable);
            var productionSchema = GetSchemaName(productionTable);
            var oldTable = $"{productionName}_old";
            var qualifiedOldTable = $"{productionSchema}.{oldTable}";

            await using var renameOldCmd = connection.CreateCommand();
            renameOldCmd.Transaction = transaction;
            renameOldCmd.CommandText = $"ALTER TABLE {productionTable} RENAME TO {oldTable}";
            await renameOldCmd.ExecuteNonQueryAsync(ct);

            await using var renameShadowCmd = connection.CreateCommand();
            renameShadowCmd.Transaction = transaction;
            renameShadowCmd.CommandText = $"ALTER TABLE {shadowTable} RENAME TO {productionName}";
            await renameShadowCmd.ExecuteNonQueryAsync(ct);

            await transaction.CommitAsync(ct);

            _logger.LogInformation("Tables swapped successfully");

            await using var dropCmd = connection.CreateCommand();
            dropCmd.CommandText = $"DROP TABLE IF EXISTS {qualifiedOldTable} CASCADE";
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

        "OutboundOrderSummary" =>
            "\"Id\"::text || ':' || COALESCE(\"OrderNumber\",'') || ':' || " +
            "COALESCE(\"Type\",'') || ':' || COALESCE(\"Status\",'') || ':' || " +
            "COALESCE(\"CustomerName\",'') || ':' || COALESCE(\"ItemCount\"::text,'0') || ':' || " +
            "COALESCE(\"OrderDate\"::text,'') || ':' || COALESCE(\"RequestedShipDate\"::text,'') || ':' || " +
            "COALESCE(\"PackedAt\"::text,'') || ':' || COALESCE(\"ShippedAt\"::text,'') || ':' || " +
            "COALESCE(\"ShipmentId\"::text,'') || ':' || COALESCE(\"ShipmentNumber\",'') || ':' || " +
            "COALESCE(\"TrackingNumber\",'')",

        "ShipmentSummary" =>
            "\"Id\"::text || ':' || COALESCE(\"ShipmentNumber\",'') || ':' || " +
            "COALESCE(\"OutboundOrderId\"::text,'') || ':' || COALESCE(\"OutboundOrderNumber\",'') || ':' || " +
            "COALESCE(\"CustomerName\",'') || ':' || COALESCE(\"Carrier\",'') || ':' || " +
            "COALESCE(\"TrackingNumber\",'') || ':' || COALESCE(\"Status\",'') || ':' || " +
            "COALESCE(\"PackedAt\"::text,'') || ':' || COALESCE(\"DispatchedAt\"::text,'') || ':' || " +
            "COALESCE(\"DeliveredAt\"::text,'') || ':' || COALESCE(\"PackedBy\",'') || ':' || " +
            "COALESCE(\"DispatchedBy\",'')",

        "DispatchHistory" =>
            "\"Id\"::text || ':' || COALESCE(\"ShipmentId\"::text,'') || ':' || " +
            "COALESCE(\"ShipmentNumber\",'') || ':' || COALESCE(\"OutboundOrderNumber\",'') || ':' || " +
            "COALESCE(\"Carrier\",'') || ':' || COALESCE(\"TrackingNumber\",'') || ':' || " +
            "COALESCE(\"VehicleId\",'') || ':' || COALESCE(\"DispatchedAt\"::text,'') || ':' || " +
            "COALESCE(\"DispatchedBy\",'') || ':' || COALESCE(\"ManualTracking\"::text,'')",

        "OnHandValue" =>
            "\"Id\"::text || ':' || COALESCE(\"ItemId\"::text,'') || ':' || " +
            "COALESCE(\"ItemSku\",'') || ':' || COALESCE(\"ItemName\",'') || ':' || " +
            "COALESCE(\"CategoryId\"::text,'') || ':' || COALESCE(\"CategoryName\",'') || ':' || " +
            "COALESCE(\"Qty\"::text,'0') || ':' || COALESCE(\"UnitCost\"::text,'0') || ':' || " +
            "COALESCE(\"TotalValue\"::text,'0') || ':' || COALESCE(\"LastUpdated\"::text,'')",

        "InboundShipmentSummary" =>
            "id || ':' || COALESCE(data->>'shipmentId','0') || ':' || " +
            "COALESCE(data->>'referenceNumber','') || ':' || " +
            "COALESCE(data->>'supplierId','0') || ':' || " +
            "COALESCE(data->>'supplierName','') || ':' || " +
            "COALESCE(data->>'totalExpectedQty','0') || ':' || " +
            "COALESCE(data->>'totalReceivedQty','0') || ':' || " +
            "COALESCE(data->>'completionPercent','0') || ':' || " +
            "COALESCE(data->>'totalLines','0') || ':' || " +
            "COALESCE(data->>'status','') || ':' || " +
            "COALESCE(data->>'expectedDate','') || ':' || " +
            "COALESCE(data->>'createdAt','') || ':' || " +
            "COALESCE(data->>'lastUpdated','')",

        _ => "id || data::text"
    };

    private static string GetFieldBasedOrderSql(string projectionName) => projectionName switch
    {
        "OutboundOrderSummary" => "\"Id\"",
        "ShipmentSummary" => "\"Id\"",
        "DispatchHistory" => "\"Id\"",
        "OnHandValue" => "\"Id\"",
        _ => "id"
    };

    private async Task<string> ComputeFieldChecksumAsync(
        NpgsqlConnection connection,
        string tableName,
        string projectionName,
        string fieldExpression,
        CancellationToken ct)
    {
        _logger.LogInformation("Computing field-based checksum for {TableName}", tableName);

        await using var cmd = connection.CreateCommand();
        var orderSql = GetFieldBasedOrderSql(projectionName);
        cmd.CommandText = $@"
            SELECT COALESCE(
                MD5(STRING_AGG({fieldExpression}, '|' ORDER BY {orderSql})),
                'empty'
            ) as checksum
            FROM {tableName}";

        var checksum = (string?)await cmd.ExecuteScalarAsync(ct) ?? "empty";

        _logger.LogInformation("Checksum for {TableName}: {Checksum}", tableName, checksum);
        return checksum;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Inbound shipment summary rebuild
    // ═══════════════════════════════════════════════════════════════════

    private async Task<int> ReplayInboundShipmentSummaryAsync(
        string shadowTable,
        NpgsqlConnection writeConnection,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Replaying InboundShipmentSummary events in GLOBAL sequence order (V-5 Rule A)");

        await using var session = _documentStore.QuerySession();
        var allEvents = await session.Events
            .QueryAllRawEvents()
            .OrderBy(e => e.Sequence)
            .ToListAsync(ct);

        var views = new Dictionary<string, InboundShipmentSummaryView>();
        var processed = 0;

        foreach (var rawEvent in allEvents)
        {
            switch (rawEvent.Data)
            {
                case InboundShipmentCreatedEvent created:
                {
                    var id = InboundShipmentSummaryView.ComputeId(created.ShipmentId);
                    var timestamp = DateTime.SpecifyKind(created.Timestamp, DateTimeKind.Utc);
                    views[id] = new InboundShipmentSummaryView
                    {
                        Id = id,
                        ShipmentId = created.ShipmentId,
                        ReferenceNumber = created.ReferenceNumber,
                        SupplierId = created.SupplierId,
                        SupplierName = created.SupplierName,
                        TotalExpectedQty = created.TotalExpectedQty,
                        TotalReceivedQty = 0m,
                        CompletionPercent = 0m,
                        TotalLines = created.TotalLines,
                        Status = "Draft",
                        ExpectedDate = created.ExpectedDate,
                        CreatedAt = new DateTimeOffset(timestamp),
                        LastUpdated = new DateTimeOffset(timestamp)
                    };

                    processed++;
                    break;
                }

                case GoodsReceivedEvent received:
                {
                    var id = InboundShipmentSummaryView.ComputeId(received.ShipmentId);
                    if (!views.TryGetValue(id, out var view))
                    {
                        var fallbackTimestamp = DateTime.SpecifyKind(received.Timestamp, DateTimeKind.Utc);
                        view = new InboundShipmentSummaryView
                        {
                            Id = id,
                            ShipmentId = received.ShipmentId,
                            SupplierId = received.SupplierId ?? 0,
                            CreatedAt = new DateTimeOffset(fallbackTimestamp),
                            LastUpdated = new DateTimeOffset(fallbackTimestamp)
                        };
                        views[id] = view;
                    }

                    view.TotalReceivedQty += received.ReceivedQty;
                    view.CompletionPercent = view.TotalExpectedQty <= 0m
                        ? 0m
                        : Math.Min(1m, view.TotalReceivedQty / view.TotalExpectedQty);
                    view.Status = view.TotalReceivedQty switch
                    {
                        <= 0m => "Draft",
                        _ when view.TotalReceivedQty >= view.TotalExpectedQty => "Complete",
                        _ => "Partial"
                    };

                    var timestamp = DateTime.SpecifyKind(received.Timestamp, DateTimeKind.Utc);
                    view.LastUpdated = new DateTimeOffset(timestamp);

                    processed++;
                    break;
                }
            }
        }

        await InsertDocumentsToShadowAsync(shadowTable, views.Values, writeConnection, ct);

        _logger.LogInformation(
            "InboundShipmentSummary: replayed {EventCount} events → {RecordCount} records",
            processed,
            views.Count);

        return processed;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Outbound summary table rebuilds
    // ═══════════════════════════════════════════════════════════════════

    private async Task<int> ReplayOutboundOrderSummaryAsync(
        string shadowTable,
        NpgsqlConnection writeConnection,
        CancellationToken ct)
    {
        _logger.LogInformation("Rebuilding outbound order summary into {ShadowTable}", shadowTable);

        await using var cmd = writeConnection.CreateCommand();
        cmd.CommandText = $@"
            INSERT INTO {shadowTable}
            (
                ""Id"", ""OrderNumber"", ""Type"", ""Status"", ""CustomerName"", ""ItemCount"",
                ""OrderDate"", ""RequestedShipDate"", ""PackedAt"", ""ShippedAt"",
                ""ShipmentId"", ""ShipmentNumber"", ""TrackingNumber""
            )
            SELECT
                o.""Id"",
                o.""OrderNumber"",
                o.""Type"",
                o.""Status"",
                c.""Name"" AS ""CustomerName"",
                COALESCE(ol.""ItemCount"", 0) AS ""ItemCount"",
                o.""OrderDate"",
                o.""RequestedShipDate"",
                o.""PackedAt"",
                o.""ShippedAt"",
                o.""ShipmentId"",
                s.""ShipmentNumber"",
                s.""TrackingNumber""
            FROM public.outbound_orders o
            LEFT JOIN public.sales_orders so ON so.""Id"" = o.""SalesOrderId""
            LEFT JOIN public.customers c ON c.""Id"" = so.""CustomerId""
            LEFT JOIN public.shipments s ON s.""Id"" = o.""ShipmentId""
            LEFT JOIN
            (
                SELECT ""OutboundOrderId"", COUNT(1)::int AS ""ItemCount""
                FROM public.outbound_order_lines
                GROUP BY ""OutboundOrderId""
            ) ol ON ol.""OutboundOrderId"" = o.""Id""
            WHERE o.""IsDeleted"" = false;";

        return await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<int> ReplayShipmentSummaryAsync(
        string shadowTable,
        NpgsqlConnection writeConnection,
        CancellationToken ct)
    {
        _logger.LogInformation("Rebuilding shipment summary into {ShadowTable}", shadowTable);

        await using var cmd = writeConnection.CreateCommand();
        cmd.CommandText = $@"
            INSERT INTO {shadowTable}
            (
                ""Id"", ""ShipmentNumber"", ""OutboundOrderId"", ""OutboundOrderNumber"",
                ""CustomerName"", ""Carrier"", ""TrackingNumber"", ""Status"",
                ""PackedAt"", ""DispatchedAt"", ""DeliveredAt"", ""PackedBy"", ""DispatchedBy""
            )
            SELECT
                s.""Id"",
                s.""ShipmentNumber"",
                s.""OutboundOrderId"",
                o.""OrderNumber"" AS ""OutboundOrderNumber"",
                c.""Name"" AS ""CustomerName"",
                s.""Carrier"",
                s.""TrackingNumber"",
                s.""Status"",
                s.""PackedAt"",
                s.""DispatchedAt"",
                s.""DeliveredAt"",
                NULL::text AS ""PackedBy"",
                dh.""DispatchedBy""
            FROM public.shipments s
            INNER JOIN public.outbound_orders o ON o.""Id"" = s.""OutboundOrderId""
            LEFT JOIN public.sales_orders so ON so.""Id"" = o.""SalesOrderId""
            LEFT JOIN public.customers c ON c.""Id"" = so.""CustomerId""
            LEFT JOIN LATERAL
            (
                SELECT d.""DispatchedBy""
                FROM public.dispatch_history d
                WHERE d.""ShipmentId"" = s.""Id""
                ORDER BY d.""DispatchedAt"" DESC
                LIMIT 1
            ) dh ON TRUE
            WHERE s.""IsDeleted"" = false;";

        return await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<int> ReplayDispatchHistoryAsync(
        string shadowTable,
        NpgsqlConnection writeConnection,
        CancellationToken ct)
    {
        _logger.LogInformation("Rebuilding dispatch history into {ShadowTable}", shadowTable);

        await using var cmd = writeConnection.CreateCommand();
        cmd.CommandText = $@"
            INSERT INTO {shadowTable}
            (
                ""Id"", ""ShipmentId"", ""ShipmentNumber"", ""OutboundOrderNumber"",
                ""Carrier"", ""TrackingNumber"", ""VehicleId"", ""DispatchedAt"",
                ""DispatchedBy"", ""ManualTracking""
            )
            SELECT
                ""Id"", ""ShipmentId"", ""ShipmentNumber"", ""OutboundOrderNumber"",
                ""Carrier"", ""TrackingNumber"", ""VehicleId"", ""DispatchedAt"",
                ""DispatchedBy"", ""ManualTracking""
            FROM public.dispatch_history;";

        return await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<int> ReplayOnHandValueAsync(
        string shadowTable,
        NpgsqlConnection writeConnection,
        CancellationToken ct)
    {
        _logger.LogInformation("Rebuilding on-hand value projection into {ShadowTable}", shadowTable);

        await using var querySession = _documentStore.QuerySession();
        var allEvents = await querySession.Events
            .QueryAllRawEvents()
            .OrderBy(e => e.Sequence)
            .ToListAsync(ct);

        var unitCostByItemId = new Dictionary<int, decimal>();
        var valuationUpdatedAtByItemId = new Dictionary<int, DateTime>();
        var processedEvents = 0;

        foreach (var rawEvent in allEvents)
        {
            switch (rawEvent.Data)
            {
                case ValuationInitialized initialized when Domain.Aggregates.Valuation.TryToInventoryItemId(initialized.ItemId, out var initializedItemId):
                    unitCostByItemId[initializedItemId] = decimal.Round(initialized.InitialUnitCost, 4, MidpointRounding.AwayFromZero);
                    valuationUpdatedAtByItemId[initializedItemId] = DateTime.SpecifyKind(initialized.Timestamp, DateTimeKind.Utc);
                    processedEvents++;
                    break;
                case CostAdjusted adjusted when Domain.Aggregates.Valuation.TryToInventoryItemId(adjusted.ItemId, out var adjustedItemId):
                    unitCostByItemId[adjustedItemId] = decimal.Round(adjusted.NewUnitCost, 4, MidpointRounding.AwayFromZero);
                    valuationUpdatedAtByItemId[adjustedItemId] = DateTime.SpecifyKind(adjusted.Timestamp, DateTimeKind.Utc);
                    processedEvents++;
                    break;
                case LandedCostAllocated landed when Domain.Aggregates.Valuation.TryToInventoryItemId(landed.ItemId, out var landedItemId):
                    unitCostByItemId[landedItemId] = decimal.Round(landed.NewUnitCost, 4, MidpointRounding.AwayFromZero);
                    valuationUpdatedAtByItemId[landedItemId] = DateTime.SpecifyKind(landed.Timestamp, DateTimeKind.Utc);
                    processedEvents++;
                    break;
                case StockWrittenDown writtenDown when Domain.Aggregates.Valuation.TryToInventoryItemId(writtenDown.ItemId, out var writtenDownItemId):
                    unitCostByItemId[writtenDownItemId] = decimal.Round(writtenDown.NewUnitCost, 4, MidpointRounding.AwayFromZero);
                    valuationUpdatedAtByItemId[writtenDownItemId] = DateTime.SpecifyKind(writtenDown.Timestamp, DateTimeKind.Utc);
                    processedEvents++;
                    break;
            }
        }

        var availableRows = await querySession.Query<AvailableStockView>()
            .ToListAsync(ct);
        var qtyByItemId = availableRows
            .Where(x => x.ItemId.HasValue)
            .GroupBy(x => x.ItemId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.AvailableQty));
        var stockUpdatedAtByItemId = availableRows
            .Where(x => x.ItemId.HasValue)
            .GroupBy(x => x.ItemId!.Value)
            .ToDictionary(g => g.Key, g => g.Max(x => DateTime.SpecifyKind(x.LastUpdated, DateTimeKind.Utc)));
        var qtyBySku = availableRows
            .Where(x => !string.IsNullOrWhiteSpace(x.SKU))
            .GroupBy(x => x.SKU, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.AvailableQty), StringComparer.OrdinalIgnoreCase);
        var stockUpdatedAtBySku = availableRows
            .Where(x => !string.IsNullOrWhiteSpace(x.SKU))
            .GroupBy(x => x.SKU, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Max(x => DateTime.SpecifyKind(x.LastUpdated, DateTimeKind.Utc)),
                StringComparer.OrdinalIgnoreCase);

        await using var itemsCmd = writeConnection.CreateCommand();
        itemsCmd.CommandText = @"
            SELECT i.""Id"", i.""InternalSKU"", i.""Name"", i.""CategoryId"", c.""Name""
            FROM public.items i
            LEFT JOIN public.item_categories c ON c.""Id"" = i.""CategoryId""
            ORDER BY i.""Id""";

        await using var reader = await itemsCmd.ExecuteReaderAsync(ct);
        var rows = new List<(int ItemId, string Sku, string ItemName, int? CategoryId, string? CategoryName)>();
        while (await reader.ReadAsync(ct))
        {
            rows.Add((
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        }
        await reader.CloseAsync();

        var inserted = 0;
        foreach (var item in rows)
        {
            if (!unitCostByItemId.TryGetValue(item.ItemId, out var unitCost))
            {
                continue;
            }

            var qty = qtyByItemId.TryGetValue(item.ItemId, out var qtyById)
                ? qtyById
                : qtyBySku.TryGetValue(item.Sku, out var qtyBySkuValue)
                    ? qtyBySkuValue
                    : 0m;

            var totalValue = decimal.Round(qty * unitCost, 4, MidpointRounding.AwayFromZero);
            var id = Domain.Aggregates.Valuation.ToValuationItemId(item.ItemId);
            var updatedAt = ResolveUpdatedAt(
                item.ItemId,
                item.Sku,
                valuationUpdatedAtByItemId,
                stockUpdatedAtByItemId,
                stockUpdatedAtBySku);

            await using var insertCmd = writeConnection.CreateCommand();
            insertCmd.CommandText = $@"
                INSERT INTO {shadowTable}
                (
                    ""Id"", ""ItemId"", ""ItemSku"", ""ItemName"", ""CategoryId"",
                    ""CategoryName"", ""Qty"", ""UnitCost"", ""TotalValue"", ""LastUpdated""
                )
                VALUES
                (
                    @id, @itemId, @itemSku, @itemName, @categoryId,
                    @categoryName, @qty, @unitCost, @totalValue, @lastUpdated
                )";
            insertCmd.Parameters.AddWithValue("id", id);
            insertCmd.Parameters.AddWithValue("itemId", item.ItemId);
            insertCmd.Parameters.AddWithValue("itemSku", item.Sku);
            insertCmd.Parameters.AddWithValue("itemName", item.ItemName);
            insertCmd.Parameters.AddWithValue("categoryId", item.CategoryId.HasValue ? item.CategoryId.Value : DBNull.Value);
            insertCmd.Parameters.AddWithValue("categoryName", item.CategoryName ?? (object)DBNull.Value);
            insertCmd.Parameters.AddWithValue("qty", qty);
            insertCmd.Parameters.AddWithValue("unitCost", unitCost);
            insertCmd.Parameters.AddWithValue("totalValue", totalValue);
            insertCmd.Parameters.AddWithValue("lastUpdated", updatedAt);
            await insertCmd.ExecuteNonQueryAsync(ct);
            inserted++;
        }

        _logger.LogInformation(
            "OnHandValue: replayed {EventCount} valuation events and inserted {RecordCount} rows",
            processedEvents,
            inserted);

        return processedEvents;
    }

    private static DateTime ResolveUpdatedAt(
        int itemId,
        string sku,
        IReadOnlyDictionary<int, DateTime> valuationUpdatedAtByItemId,
        IReadOnlyDictionary<int, DateTime> stockUpdatedAtByItemId,
        IReadOnlyDictionary<string, DateTime> stockUpdatedAtBySku)
    {
        valuationUpdatedAtByItemId.TryGetValue(itemId, out var valuationUpdatedAt);
        stockUpdatedAtByItemId.TryGetValue(itemId, out var stockUpdatedAtByItem);
        stockUpdatedAtBySku.TryGetValue(sku, out var stockUpdatedAtBySkuValue);

        var updatedAt = valuationUpdatedAt;
        if (stockUpdatedAtByItem > updatedAt)
        {
            updatedAt = stockUpdatedAtByItem;
        }

        if (stockUpdatedAtBySkuValue > updatedAt)
        {
            updatedAt = stockUpdatedAtBySkuValue;
        }

        if (updatedAt == default)
        {
            updatedAt = DateTime.UnixEpoch;
        }

        return DateTime.SpecifyKind(updatedAt, DateTimeKind.Utc);
    }

    // ═══════════════════════════════════════════════════════════════════
    // LocationBalance replay
    // ═══════════════════════════════════════════════════════════════════

    private async Task<int> ReplayLocationBalanceEventsAsync(
        string shadowTable, NpgsqlConnection writeConnection, CancellationToken ct)
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
            if (processed % 1000 == 0)
            {
                _logger.LogInformation(
                    "LocationBalance rebuild progress: {Processed} events processed",
                    processed);
            }

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
        await InsertDocumentsToShadowAsync(shadowTable, balances.Values, writeConnection, ct);

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
        string shadowTable, NpgsqlConnection writeConnection, CancellationToken ct)
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
                    if (processed % 1000 == 0)
                    {
                        _logger.LogInformation(
                            "AvailableStock rebuild progress: {Processed} events processed",
                            processed);
                    }
                    break;

                case PickingStartedEvent pickingStarted:
                    ApplyPickingStartedToAvailableStock(pickingStarted, views);
                    processed++;
                    if (processed % 1000 == 0)
                    {
                        _logger.LogInformation(
                            "AvailableStock rebuild progress: {Processed} events processed",
                            processed);
                    }
                    break;

                case ReservationConsumedEvent consumed:
                    ApplyReservationConsumedToAvailableStock(consumed, views);
                    processed++;
                    if (processed % 1000 == 0)
                    {
                        _logger.LogInformation(
                            "AvailableStock rebuild progress: {Processed} events processed",
                            processed);
                    }
                    break;

                case ReservationCancelledEvent cancelled:
                    ApplyReservationCancelledToAvailableStock(cancelled, views);
                    processed++;
                    if (processed % 1000 == 0)
                    {
                        _logger.LogInformation(
                            "AvailableStock rebuild progress: {Processed} events processed",
                            processed);
                    }
                    break;
            }
        }

        // Insert into shadow table
        await InsertDocumentsToShadowAsync(shadowTable, views.Values, writeConnection, ct);

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
        string shadowTable, IEnumerable<T> documents, NpgsqlConnection conn, CancellationToken ct)
        where T : class
    {
        // [MED-01] Use Marten's own serializer so shadow data is byte-identical
        // to production data. This avoids checksum mismatches caused by
        // different DateTime/decimal formatting between System.Text.Json defaults
        // and Marten's SystemTextJsonSerializer configuration.
        var martenSerializer = _documentStore.Options.Serializer();

        foreach (var doc in documents)
        {
            // Get the Id property via reflection (all view models have string Id)
            var idProp = typeof(T).GetProperty("Id");
            var id = idProp?.GetValue(doc)?.ToString() ?? string.Empty;

            var json = martenSerializer.ToJson(doc);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                INSERT INTO {shadowTable} (id, data)
                VALUES (@id, @data::jsonb)";
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("data", json);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task<string?> ResolveQualifiedTableNameAsync(
        NpgsqlConnection connection,
        string tableName,
        CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT n.nspname || '.' || c.relname
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind = 'r' AND c.relname = @tableName
            ORDER BY CASE WHEN n.nspname = 'public' THEN 0 ELSE 1 END, n.nspname
            LIMIT 1";
        cmd.Parameters.AddWithValue("tableName", tableName);
        return await cmd.ExecuteScalarAsync(ct) as string;
    }

    private static string BuildShadowTableName(string qualifiedProductionTable)
    {
        var schema = GetSchemaName(qualifiedProductionTable);
        var name = GetUnqualifiedTableName(qualifiedProductionTable);
        return $"{schema}.{name}_shadow";
    }

    private static string GetSchemaName(string qualifiedTableName)
    {
        var dotIndex = qualifiedTableName.IndexOf('.');
        if (dotIndex <= 0)
        {
            return "public";
        }

        return qualifiedTableName[..dotIndex];
    }

    private static string GetUnqualifiedTableName(string qualifiedTableName)
    {
        var dotIndex = qualifiedTableName.LastIndexOf('.');
        return dotIndex >= 0
            ? qualifiedTableName[(dotIndex + 1)..]
            : qualifiedTableName;
    }

    private sealed class NoOpDistributedLock : IDistributedLock
    {
        public Task<DistributedLockAcquireResult> TryAcquireAsync(
            string key,
            string holder,
            TimeSpan ttl,
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                new DistributedLockAcquireResult(
                    true,
                    new DistributedLockInfo(
                        key,
                        holder,
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow.Add(ttl))));

        public Task ReleaseAsync(
            string key,
            string holder,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<DistributedLockInfo?> GetActiveLockAsync(
            string key,
            CancellationToken cancellationToken = default)
            => Task.FromResult<DistributedLockInfo?>(null);
    }
}
