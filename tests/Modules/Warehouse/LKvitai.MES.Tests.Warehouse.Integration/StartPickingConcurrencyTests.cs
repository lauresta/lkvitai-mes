using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Domain;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Projections;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Marten;
using Marten.Events.Projections;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Integration;

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

    // ══════════════════════════════════════════════════════════════════
    // [HOTFIX CRIT-01] StartPicking vs concurrent outbound movement
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// [HOTFIX CRIT-01] Concurrent StartPicking + TransferOut on same (location, sku).
    /// Stock: 100 units. Reservation wants 80. TransferOut wants 30.
    /// If both proceed, hardLocked(80) > balance(70) — invariant violation.
    /// With advisory lock serialization, one must fail or reduce available correctly.
    ///
    /// Expected outcome:
    /// - If StartPicking goes first: hardLocked=80, then TransferOut reads balance=100
    ///   but 80 is hard-locked: should fail or reduce balance to 20 (less than 30 needed).
    /// - If TransferOut goes first: balance becomes 70, then StartPicking reads
    ///   available=70, which may or may not be enough (70 ge 80? NO: StartPicking fails).
    ///
    /// Either way: hardLockedSum ≤ balance must hold.
    /// </summary>
    [SkippableFact]
    public async Task ConcurrentStartPicking_VsTransferOut_NeverExceedsBalance()
    {
        DockerRequirement.EnsureEnabled();

        var warehouseId = "WH1";
        var location = "LOC-CRIT01";
        var sku = "SKU-CRIT01";
        var stockQty = 100m;
        var reservationQty = 80m;
        var transferOutQty = 30m;

        // Seed stock
        await SeedStockLedger(warehouseId, location, sku, stockQty);

        // Create allocated reservation wanting 80 units
        var reservationId = Guid.NewGuid();
        await CreateAllocatedReservation(reservationId, warehouseId, location, sku, reservationQty);

        // Build services for MediatR handler path
        var config = BuildConfiguration();
        var services = new ServiceCollection();
        services.AddSingleton<IDocumentStore>(_store!);
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IBalanceGuardLockFactory, PostgresBalanceGuardLockFactory>();
        services.AddScoped<IStockLedgerRepository, MartenStockLedgerRepository>();
        services.AddLogging();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<RecordStockMovementCommandHandler>();
        });

        await using var sp = services.BuildServiceProvider();

        // ── Act: Run StartPicking + TransferOut concurrently ──
        var logger = NullLogger<MartenStartPickingOrchestration>.Instance;
        var orch = new MartenStartPickingOrchestration(_store!, config, logger);

        var startPickingTask = orch.StartPickingAsync(
            reservationId, Guid.NewGuid(), CancellationToken.None);

        Task<Result> transferOutTask;
        using (var scope = sp.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            transferOutTask = mediator.Send(new RecordStockMovementCommand
            {
                WarehouseId = warehouseId,
                SKU = sku,
                Quantity = transferOutQty,
                FromLocation = location,
                ToLocation = "LOC-ELSEWHERE",
                MovementType = MovementType.Transfer,
                OperatorId = Guid.NewGuid()
            });

            var results = await Task.WhenAll(startPickingTask, transferOutTask);

            // ── Assert: invariant hardLockedSum ≤ balance must hold ──
            // At least one operation should fail (not enough stock for both)
            await using var session = _store!.LightweightSession();

            // Check current balance
            var ledgerStreamId = StockLedgerStreamId.For(warehouseId, location, sku);
            var ledger = await session.Events.AggregateStreamAsync<StockLedger>(
                ledgerStreamId) ?? new StockLedger();
            var currentBalance = ledger.GetBalance(location, sku);

            // Check hard locks
            await using var querySession = _store.QuerySession();
            var hardLocks = await querySession.Query<ActiveHardLockView>()
                .Where(x => x.Location == location && x.SKU == sku)
                .ToListAsync();
            var totalHardLocked = hardLocks.Sum(x => x.HardLockedQty);

            // THE INVARIANT: hardLockedSum must never exceed balance
            totalHardLocked.Should().BeLessThanOrEqualTo(currentBalance,
                $"CRIT-01 INVARIANT: hardLockedSum({totalHardLocked}) must be ≤ " +
                $"balance({currentBalance})");

            // Verify at least one succeeded (the test is meaningful)
            var anySuccess = results.Any(r => r.IsSuccess);
            anySuccess.Should().BeTrue("at least one operation should succeed");
        }
    }

    // ── Helper methods ───────────────────────────────────────────────

    private async Task SeedStockLedger(
        string warehouseId, string location, string sku, decimal quantity)
    {
        await using var session = _store!.LightweightSession();

        var streamId = LKvitai.MES.Modules.Warehouse.Domain.StockLedgerStreamId.For(warehouseId, location, sku);
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
