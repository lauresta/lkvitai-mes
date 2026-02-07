using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Domain.Aggregates;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.Projections;
using Marten;
using Marten.Events.Projections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

/// <summary>
/// Integration tests for concurrent StartPicking operations [MITIGATION R-3 / RISK-01].
///
/// Uses Testcontainers to spin up a real PostgreSQL instance.
/// Verifies that when two reservations compete for the same stock,
/// one succeeds and the other fails — no double-booking.
///
/// These tests are opt-in. Set <c>TESTCONTAINERS_ENABLED=1</c> to run them.
/// </summary>
public class StartPickingConcurrencyTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private IDocumentStore? _store;
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        if (!DockerRequirement.IsEnabled)
            return;

        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();

        await _postgres.StartAsync();
        _connectionString = _postgres.GetConnectionString();

        _store = DocumentStore.For(opts =>
        {
            opts.Connection(_connectionString);
            opts.Events.StreamIdentity = Marten.Events.StreamIdentity.AsString;
            opts.Events.DatabaseSchemaName = "warehouse_events";

            // Register the ActiveHardLocks inline projection
            opts.Projections.Add<ActiveHardLocksProjection>(ProjectionLifecycle.Inline);
        });

        // Ensure schema is created
        await _store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        _store?.Dispose();
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }

    /// <summary>
    /// Two reservations target the same stock (only enough for one).
    /// Both call StartPicking concurrently.
    /// Exactly one should succeed, the other should fail with insufficient stock.
    /// </summary>
    [SkippableFact]
    public async Task ConcurrentStartPicking_SameStock_OnlyOneSucceeds()
    {
        DockerRequirement.EnsureEnabled();

        // ── Arrange: seed stock and create two allocated reservations ──
        var warehouseId = "WH1";
        var location = "LOC-A";
        var sku = "SKU-001";
        var stockQty = 100m;

        // Seed the StockLedger with 100 units
        await SeedStockLedger(warehouseId, location, sku, stockQty);

        // Create Reservation 1: wants 70 units (would leave 30)
        var reservation1Id = Guid.NewGuid();
        await CreateAllocatedReservation(reservation1Id, warehouseId, location, sku, 70m);

        // Create Reservation 2: wants 50 units (only 30 left if R1 succeeds)
        var reservation2Id = Guid.NewGuid();
        await CreateAllocatedReservation(reservation2Id, warehouseId, location, sku, 50m);

        // ── Act: Start picking concurrently ──
        var config = BuildConfiguration();
        var logger = NullLogger<MartenStartPickingOrchestration>.Instance;
        var orch1 = new MartenStartPickingOrchestration(_store!, config, logger);
        var orch2 = new MartenStartPickingOrchestration(_store!, config, logger);

        var task1 = orch1.StartPickingAsync(reservation1Id, Guid.NewGuid(), CancellationToken.None);
        var task2 = orch2.StartPickingAsync(reservation2Id, Guid.NewGuid(), CancellationToken.None);

        var results = await Task.WhenAll(task1, task2);

        // ── Assert: exactly one succeeds, one fails ──
        var successes = results.Where(r => r.IsSuccess).ToList();
        var failures = results.Where(r => !r.IsSuccess).ToList();

        successes.Should().HaveCount(1, "exactly one StartPicking should succeed");
        failures.Should().HaveCount(1, "exactly one StartPicking should fail");
        failures[0].Error.Should().Contain("Insufficient",
            "the failure should be due to insufficient stock");
    }

    /// <summary>
    /// Two reservations target different stock (both have enough).
    /// Both should succeed — advisory locks should not block unrelated stock.
    /// </summary>
    [SkippableFact]
    public async Task ConcurrentStartPicking_DifferentStock_BothSucceed()
    {
        DockerRequirement.EnsureEnabled();

        // ── Arrange ──
        var warehouseId = "WH1";

        // Stock at LOC-A: 100 units of SKU-001
        await SeedStockLedger(warehouseId, "LOC-A", "SKU-001", 100m);
        // Stock at LOC-B: 100 units of SKU-002
        await SeedStockLedger(warehouseId, "LOC-B", "SKU-002", 100m);

        var reservation1Id = Guid.NewGuid();
        await CreateAllocatedReservation(reservation1Id, warehouseId, "LOC-A", "SKU-001", 50m);

        var reservation2Id = Guid.NewGuid();
        await CreateAllocatedReservation(reservation2Id, warehouseId, "LOC-B", "SKU-002", 50m);

        // ── Act ──
        var config = BuildConfiguration();
        var logger = NullLogger<MartenStartPickingOrchestration>.Instance;
        var orch1 = new MartenStartPickingOrchestration(_store!, config, logger);
        var orch2 = new MartenStartPickingOrchestration(_store!, config, logger);

        var task1 = orch1.StartPickingAsync(reservation1Id, Guid.NewGuid(), CancellationToken.None);
        var task2 = orch2.StartPickingAsync(reservation2Id, Guid.NewGuid(), CancellationToken.None);

        var results = await Task.WhenAll(task1, task2);

        // ── Assert: both succeed ──
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue());
    }

    /// <summary>
    /// Starting picking on a reservation that doesn't exist should return failure.
    /// </summary>
    [SkippableFact]
    public async Task StartPicking_NonExistentReservation_ShouldFail()
    {
        DockerRequirement.EnsureEnabled();

        var config = BuildConfiguration();
        var logger = NullLogger<MartenStartPickingOrchestration>.Instance;
        var orch = new MartenStartPickingOrchestration(_store!, config, logger);

        var result = await orch.StartPickingAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    /// <summary>
    /// Starting picking on a reservation that is not ALLOCATED should return failure.
    /// </summary>
    [SkippableFact]
    public async Task StartPicking_PendingReservation_ShouldFail()
    {
        DockerRequirement.EnsureEnabled();

        var reservationId = Guid.NewGuid();
        var streamId = Reservation.StreamIdFor(reservationId);

        // Create reservation but don't allocate it
        await using var session = _store!.LightweightSession();
        session.Events.StartStream<Reservation>(streamId, new ReservationCreatedEvent
        {
            ReservationId = reservationId,
            Purpose = "test",
            Priority = 1,
            RequestedLines = new List<ReservationLineDto>
            {
                new() { SKU = "SKU-001", Quantity = 10m }
            }
        });
        await session.SaveChangesAsync();

        var config = BuildConfiguration();
        var logger = NullLogger<MartenStartPickingOrchestration>.Instance;
        var orch = new MartenStartPickingOrchestration(_store!, config, logger);

        var result = await orch.StartPickingAsync(reservationId, Guid.NewGuid(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("ALLOCATED");
    }

    /// <summary>
    /// After successful StartPicking, the ActiveHardLockView rows should exist.
    /// </summary>
    [SkippableFact]
    public async Task StartPicking_Success_ShouldCreateActiveHardLockRows()
    {
        DockerRequirement.EnsureEnabled();

        var warehouseId = "WH1";
        var location = "LOC-C";
        var sku = "SKU-010";
        var qty = 25m;

        await SeedStockLedger(warehouseId, location, sku, 100m);

        var reservationId = Guid.NewGuid();
        await CreateAllocatedReservation(reservationId, warehouseId, location, sku, qty);

        var config = BuildConfiguration();
        var logger = NullLogger<MartenStartPickingOrchestration>.Instance;
        var orch = new MartenStartPickingOrchestration(_store!, config, logger);

        var result = await orch.StartPickingAsync(reservationId, Guid.NewGuid(), CancellationToken.None);
        result.IsSuccess.Should().BeTrue();

        // Verify ActiveHardLockView rows were created by inline projection
        await using var querySession = _store!.QuerySession();
        var locks = await querySession.Query<ActiveHardLockView>()
            .Where(x => x.ReservationId == reservationId)
            .ToListAsync();

        locks.Should().HaveCount(1);
        locks[0].Location.Should().Be(location);
        locks[0].SKU.Should().Be(sku);
        locks[0].HardLockedQty.Should().Be(qty);
        locks[0].WarehouseId.Should().Be(warehouseId);
    }

    // ── Helper methods ───────────────────────────────────────────────

    private async Task SeedStockLedger(
        string warehouseId, string location, string sku, decimal quantity)
    {
        await using var session = _store!.LightweightSession();

        var streamId = LKvitai.MES.Domain.StockLedgerStreamId.For(warehouseId, location, sku);
        session.Events.StartStream<StockLedger>(streamId, new StockMovedEvent
        {
            MovementId = Guid.NewGuid(),
            SKU = sku,
            Quantity = quantity,
            FromLocation = "",
            ToLocation = location,
            MovementType = "RECEIPT",
            OperatorId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        });
        await session.SaveChangesAsync();
    }

    private async Task CreateAllocatedReservation(
        Guid reservationId, string warehouseId, string location, string sku, decimal quantity)
    {
        await using var session = _store!.LightweightSession();

        var streamId = Reservation.StreamIdFor(reservationId);

        session.Events.StartStream<Reservation>(streamId,
            new ReservationCreatedEvent
            {
                ReservationId = reservationId,
                Purpose = "test",
                Priority = 1,
                RequestedLines = new List<ReservationLineDto>
                {
                    new() { SKU = sku, Quantity = quantity }
                }
            },
            new StockAllocatedEvent
            {
                ReservationId = reservationId,
                LockType = "SOFT",
                Allocations = new List<AllocationDto>
                {
                    new()
                    {
                        SKU = sku,
                        Quantity = quantity,
                        Location = location,
                        WarehouseId = warehouseId,
                        HandlingUnitIds = new List<Guid> { Guid.NewGuid() }
                    }
                }
            });

        await session.SaveChangesAsync();
    }

    private IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:WarehouseDb"] = _connectionString
            })
            .Build();
    }
}
