using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Domain;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Projections;
using LKvitai.MES.Projections;
using Marten;
using Marten.Events.Projections;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

/// <summary>
/// Integration tests for AvailableStock projection rebuild (V-5 / HOTFIX H).
///
/// Validates:
///   - Shadow table creation + event replay + checksum verification + atomic swap
///   - Multi-stream events (StockMoved + PickingStarted + ReservationConsumed) replayed
///     in GLOBAL sequence order
///   - Field-based checksum (MED-01 fix) matches between live and shadow
///   - Rebuild result matches live async projection
///
/// Docker-gated: TESTCONTAINERS_ENABLED=1
/// </summary>
public class AvailableStockRebuildTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private IDocumentStore? _store;

    private const string WarehouseId = "WH1";
    private const string Location = "LOC-RB-A";
    private const string Sku = "SKU-RB-001";

    public async Task InitializeAsync()
    {
        if (!DockerRequirement.IsEnabled) return;

        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();

        await _postgres.StartAsync();

        _store = DocumentStore.For(opts =>
        {
            opts.Connection(_postgres.GetConnectionString());
            opts.Events.StreamIdentity = Marten.Events.StreamIdentity.AsString;

            // Register the AvailableStock projection as Async (matches production)
            opts.Projections.Add<AvailableStockProjection>(ProjectionLifecycle.Async);
        });

        await _store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        _store?.Dispose();
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Full lifecycle: Receipt + Lock + Pick + Consume → rebuild matches
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Seeds a full lifecycle of events (receipt, hard lock, pick movement, consume)
    /// across multiple streams, runs the live async projection, then rebuilds and
    /// verifies checksum match + correct final values.
    /// </summary>
    [SkippableFact]
    public async Task RebuildAvailableStock_FullLifecycle_MatchesLiveProjection()
    {
        DockerRequirement.EnsureEnabled();

        // ── Seed events ──────────────────────────────────────────────
        var stockStreamKey = StockLedgerStreamId.For(WarehouseId, Location, Sku);
        var reservationId = Guid.NewGuid();
        var reservationStreamKey = $"reservation-{reservationId}";

        await using (var session = _store!.LightweightSession())
        {
            // 1. Receipt: 500 units from SUPPLIER → LOC-RB-A
            session.Events.Append(stockStreamKey, new StockMovedEvent
            {
                MovementId = Guid.NewGuid(),
                SKU = Sku,
                Quantity = 500m,
                FromLocation = "SUPPLIER",
                ToLocation = Location,
                MovementType = "RECEIPT",
                OperatorId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow
            });

            // 2. PickingStarted: 150 units hard-locked
            session.Events.Append(reservationStreamKey, new PickingStartedEvent
            {
                ReservationId = reservationId,
                LockType = "HARD",
                Timestamp = DateTime.UtcNow,
                HardLockedLines = new List<HardLockLineDto>
                {
                    new()
                    {
                        WarehouseId = WarehouseId,
                        Location = Location,
                        SKU = Sku,
                        HardLockedQty = 150m
                    }
                }
            });

            // 3. StockMoved (pick): 150 units → PRODUCTION
            session.Events.Append(stockStreamKey, new StockMovedEvent
            {
                MovementId = Guid.NewGuid(),
                SKU = Sku,
                Quantity = 150m,
                FromLocation = Location,
                ToLocation = "PRODUCTION",
                MovementType = "PICK",
                OperatorId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow
            });

            // 4. ReservationConsumed: hard lock released
            session.Events.Append(reservationStreamKey, new ReservationConsumedEvent
            {
                ReservationId = reservationId,
                ActualQuantity = 150m,
                Timestamp = DateTime.UtcNow,
                ReleasedHardLockLines = new List<HardLockLineDto>
                {
                    new()
                    {
                        WarehouseId = WarehouseId,
                        Location = Location,
                        SKU = Sku,
                        HardLockedQty = 150m
                    }
                }
            });

            await session.SaveChangesAsync();
        }

        // ── Run live async projection ────────────────────────────────
        using (var daemon = await _store.BuildProjectionDaemonAsync())
        {
            await daemon.StartAllAsync();
            await daemon.WaitForNonStaleData(TimeSpan.FromSeconds(15));
            await daemon.StopAllAsync();
        }

        // ── Rebuild ──────────────────────────────────────────────────
        var logger = NullLogger<ProjectionRebuildService>.Instance;
        var rebuildService = new ProjectionRebuildService(_store, logger);
        var result = await rebuildService.RebuildProjectionAsync(
            "AvailableStock", verify: true);

        // ── Assert: rebuild succeeded ────────────────────────────────
        result.IsSuccess.Should().BeTrue($"Rebuild failed: {result.Error}");
        result.Value.EventsProcessed.Should().BeGreaterThan(0);
        result.Value.ChecksumMatch.Should().BeTrue(
            "shadow rebuild must match live projection (field-based checksum)");
        result.Value.Swapped.Should().BeTrue();

        // ── Assert: final values correct after swap ──────────────────
        await using var query = _store.QuerySession();
        var viewId = AvailableStockView.ComputeId(WarehouseId, Location, Sku);
        var view = await query.LoadAsync<AvailableStockView>(viewId);

        view.Should().NotBeNull();
        // 500 received - 150 picked = 350 on-hand
        view!.OnHandQty.Should().Be(350m);
        // 150 locked - 150 released = 0 hard-locked
        view.HardLockedQty.Should().Be(0m);
        // available = 350 - 0 = 350
        view.AvailableQty.Should().Be(350m);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Rebuild with only StockMoved events (no reservation events)
    // ═══════════════════════════════════════════════════════════════════

    [SkippableFact]
    public async Task RebuildAvailableStock_StockMovedOnly_MatchesLive()
    {
        DockerRequirement.EnsureEnabled();

        var stockStreamKey = StockLedgerStreamId.For(WarehouseId, Location, Sku);

        await using (var session = _store!.LightweightSession())
        {
            session.Events.Append(stockStreamKey, new StockMovedEvent
            {
                MovementId = Guid.NewGuid(),
                SKU = Sku,
                Quantity = 250m,
                FromLocation = "SUPPLIER",
                ToLocation = Location,
                MovementType = "RECEIPT",
                OperatorId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow
            });
            await session.SaveChangesAsync();
        }

        using (var daemon = await _store.BuildProjectionDaemonAsync())
        {
            await daemon.StartAllAsync();
            await daemon.WaitForNonStaleData(TimeSpan.FromSeconds(10));
            await daemon.StopAllAsync();
        }

        var logger = NullLogger<ProjectionRebuildService>.Instance;
        var rebuildService = new ProjectionRebuildService(_store, logger);
        var result = await rebuildService.RebuildProjectionAsync(
            "AvailableStock", verify: true);

        result.IsSuccess.Should().BeTrue($"Rebuild failed: {result.Error}");
        result.Value.ChecksumMatch.Should().BeTrue();
        result.Value.Swapped.Should().BeTrue();

        await using var query = _store.QuerySession();
        var view = await query.LoadAsync<AvailableStockView>(
            AvailableStockView.ComputeId(WarehouseId, Location, Sku));

        view.Should().NotBeNull();
        view!.OnHandQty.Should().Be(250m);
        view.AvailableQty.Should().Be(250m);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Unsupported projection name returns error
    // ═══════════════════════════════════════════════════════════════════

    [SkippableFact]
    public async Task RebuildProjection_UnknownName_ReturnsError()
    {
        DockerRequirement.EnsureEnabled();

        var logger = NullLogger<ProjectionRebuildService>.Instance;
        var rebuildService = new ProjectionRebuildService(_store!, logger);
        var result = await rebuildService.RebuildProjectionAsync("NonExistent");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not implemented");
    }
}
