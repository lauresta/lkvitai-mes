using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using IDocumentStore = Marten.IDocumentStore;
using DocumentStore = Marten.DocumentStore;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Integration;

public sealed class CycleCountAdjustmentPersistenceTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private IDocumentStore? _store;
    private DbContextOptions<WarehouseDbContext>? _options;
    private Guid _lineId;

    public async Task InitializeAsync()
    {
        if (!DockerRequirement.IsEnabled) return;
        _postgres = new PostgreSqlBuilder().WithImage("pgvector/pgvector:pg16").Build();
        await _postgres.StartAsync();
        await using (var connection = new NpgsqlConnection(_postgres.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector", connection);
            await command.ExecuteNonQueryAsync();
        }
        _options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseNpgsql(_postgres.GetConnectionString(), o => o.EnableRetryOnFailure()).Options;
        await using (var db = NewDb())
        {
            await db.Database.EnsureCreatedAsync();
            db.ItemCategories.Add(new ItemCategory { Id = 1, Code = "QA", Name = "QA" });
            db.UnitOfMeasures.Add(new UnitOfMeasure { Code = "PCS", Name = "Pieces", Type = "Piece" });
            db.Items.Add(new Item { Id = 1, InternalSKU = "QA-C1", Name = "QA-C1", CategoryId = 1, BaseUoM = "PCS" });
            db.Locations.Add(new Location { Id = 1, Code = "QA-BIN", Barcode = "QA-BIN", Type = "Bin", Status = "Active" });
            var count = new CycleCount { CountNumber = "QA-C1", ScheduledDate = DateTimeOffset.UtcNow, ScheduleCommandId = Guid.NewGuid() };
            var line = new CycleCountLine
            {
                ItemId = 1, LocationId = 1, SystemQty = 20, PhysicalQty = 18, Delta = -2,
                CountedAt = DateTimeOffset.UtcNow, Status = CycleCountLineStatus.Pending, Reason = "QA-C1"
            };
            count.Lines.Add(line);
            _lineId = line.Id;
            db.CycleCounts.Add(count);
            await db.SaveChangesAsync();
        }
        _store = DocumentStore.For(o =>
        {
            o.Connection(_postgres.GetConnectionString());
            o.DatabaseSchemaName = "warehouse_events";
            o.Events.DatabaseSchemaName = "warehouse_events";
            o.Events.StreamIdentity = Marten.Events.StreamIdentity.AsString;
            o.Events.AddEventType<StockAdjustedEvent>();
        });
        await _store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        _store?.Dispose();
        if (_postgres is not null) await _postgres.DisposeAsync();
    }

    [SkippableFact]
    public async Task Approval_CommitsEventAndEfState_AndRetryDoesNotDoubleAdjustment()
    {
        DockerRequirement.EnsureEnabled();
        await using var db = NewDb();
        var line = await db.CycleCountLines.SingleAsync();
        line.Status = CycleCountLineStatus.Approved;
        line.AdjustmentApprovedAt = DateTimeOffset.UtcNow;
        var writer = new CycleCountAdjustmentWriter(db, _store!);
        var adjustment = Adjustment();
        await writer.CommitAsync([adjustment], CancellationToken.None);
        await writer.CommitAsync([adjustment], CancellationToken.None);

        await using var verifyDb = NewDb();
        (await verifyDb.CycleCountLines.SingleAsync()).Status.Should().Be(CycleCountLineStatus.Approved);
        await using var session = _store!.QuerySession();
        var events = await session.Events.FetchStreamAsync($"stock-adjustment:{_lineId:N}");
        events.Should().ContainSingle();
        events[0].Data.Should().BeOfType<StockAdjustedEvent>().Which.QtyDelta.Should().Be(-2);
    }

    [SkippableFact]
    public async Task MartenFailure_RollsBackApprovalAndLeavesNoStockEvent()
    {
        DockerRequirement.EnsureEnabled();
        // Fault injection only in the disposable local PostgreSQL fixture.
        await using (var connection = new NpgsqlConnection(_postgres!.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("""
                CREATE FUNCTION warehouse_events.qa_reject_event() RETURNS trigger AS $$
                BEGIN RAISE EXCEPTION 'QA injected event-write failure'; END;
                $$ LANGUAGE plpgsql;
                CREATE TRIGGER qa_reject_event BEFORE INSERT ON warehouse_events.mt_events
                FOR EACH ROW EXECUTE FUNCTION warehouse_events.qa_reject_event();
                """, connection);
            await command.ExecuteNonQueryAsync();
        }
        await using var db = NewDb();
        var line = await db.CycleCountLines.SingleAsync();
        line.Status = CycleCountLineStatus.Approved;
        var writer = new CycleCountAdjustmentWriter(db, _store!);
        var act = () => writer.CommitAsync([Adjustment()], CancellationToken.None);
        await act.Should().ThrowAsync<Exception>();

        await using var verifyDb = NewDb();
        (await verifyDb.CycleCountLines.SingleAsync()).Status.Should().Be(CycleCountLineStatus.Pending);
        await using var session = _store!.QuerySession();
        (await session.Events.FetchStreamAsync($"stock-adjustment:{_lineId:N}")).Should().BeEmpty();
    }

    private WarehouseDbContext NewDb() => new(_options!);
    private StockAdjustedEvent Adjustment() => new()
    {
        AdjustmentId = _lineId, WarehouseId = "WH1", ItemId = 1, SKU = "QA-C1",
        LocationId = 1, Location = "QA-BIN", QtyDelta = -2, ReasonCode = "CYCLE_COUNT", Notes = "QA-C1"
    };
}
