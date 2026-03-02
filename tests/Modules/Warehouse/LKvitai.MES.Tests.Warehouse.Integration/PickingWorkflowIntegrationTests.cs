using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Projections;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Integration;

public class PickingWorkflowIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private DbContextOptions<WarehouseDbContext>? _dbOptions;
    private IDocumentStore? _store;

    public async Task InitializeAsync()
    {
        if (!DockerRequirement.IsEnabled)
        {
            return;
        }

        _postgres = new PostgreSqlBuilder()
            .WithImage("pgvector/pgvector:pg16")
            .Build();

        await _postgres.StartAsync();

        await using (var conn = new Npgsql.NpgsqlConnection(_postgres.GetConnectionString()))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE EXTENSION IF NOT EXISTS vector;";
            await cmd.ExecuteNonQueryAsync();
        }

        _dbOptions = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _store = DocumentStore.For(opts =>
        {
            opts.Connection(_postgres.GetConnectionString());
            opts.DatabaseSchemaName = "warehouse_events";
            opts.Events.DatabaseSchemaName = "warehouse_events";
            opts.Events.StreamIdentity = Marten.Events.StreamIdentity.AsString;
            opts.RegisterProjections();
        });
        await _store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();

        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        _store?.Dispose();
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task CreatePickTask_ShouldPersistPendingTask()
    {
        DockerRequirement.EnsureEnabled();

        await SeedBaseDataAsync();

        await using var db = CreateDbContext();
        var controller = CreateController(db);

        var result = await controller.CreateTaskAsync(new PickingController.CreatePickTaskRequest("ORD-1", 1, 10m, null));

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var payload = created.Value.Should().BeOfType<PickingController.PickTaskCreatedResponse>().Subject;

        payload.Status.Should().Be("Pending");

        var task = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.PickTasks);
        task.OrderId.Should().Be("ORD-1");
        task.Status.Should().Be("Pending");
    }

    [SkippableFact]
    public async Task CompletePickTask_WrongBarcode_ShouldReturn422()
    {
        DockerRequirement.EnsureEnabled();

        await SeedBaseDataAsync();
        await SeedAvailableStockAsync(50m);

        await using var db = CreateDbContext();
        var task = await CreateTaskAsync(db, "ORD-2", 1, 5m);

        var controller = CreateController(db);
        var result = await controller.CompleteTaskAsync(
            task.TaskId,
            new PickingController.CompletePickTaskRequest(
                2,
                5m,
                null,
                "WRONG-BARCODE",
                "LOC-A01",
                "test"));

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [SkippableFact]
    public async Task CompletePickTask_ValidRequest_ShouldEmitEventAndShowHistory()
    {
        DockerRequirement.EnsureEnabled();

        await SeedBaseDataAsync();
        await SeedAvailableStockAsync(50m);

        await using var db = CreateDbContext();
        var task = await CreateTaskAsync(db, "ORD-3", 1, 5m);

        var controller = CreateController(db);
        var result = await controller.CompleteTaskAsync(
            task.TaskId,
            new PickingController.CompletePickTaskRequest(
                2,
                5m,
                null,
                "BAR-ITEM-1",
                "LOC-A01",
                "picked"));

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<PickingController.CompletePickTaskResponse>().Subject;
        payload.Status.Should().Be("Completed");

        await using var query = _store!.QuerySession();
        var stream = await query.Events.FetchStreamAsync($"pick-task:{task.TaskId:N}");
        stream.Select(x => x.Data).Should().Contain(x => x is PickCompletedEvent);

        var historyResult = await controller.GetHistoryAsync();
        var historyOk = historyResult.Should().BeOfType<OkObjectResult>().Subject;
        var history = historyOk.Value.Should().BeOfType<PickingController.PagedResponse<PickingController.PickHistoryItemDto>>().Subject;
        history.TotalCount.Should().Be(1);
    }

    [SkippableFact]
    public async Task GetLocationSuggestions_ShouldReturnFefoOrder()
    {
        DockerRequirement.EnsureEnabled();

        await SeedBaseDataAsync();

        await using (var session = _store!.LightweightSession())
        {
            session.Store(new AvailableStockView
            {
                Id = AvailableStockView.ComputeId("WH1", "A-02", "RM-0001"),
                WarehouseId = "WH1",
                Location = "A-02",
                SKU = "RM-0001",
                AvailableQty = 5m,
                OnHandQty = 5m,
                ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(2)),
                LastUpdated = DateTime.UtcNow
            });
            session.Store(new AvailableStockView
            {
                Id = AvailableStockView.ComputeId("WH1", "A-03", "RM-0001"),
                WarehouseId = "WH1",
                Location = "A-03",
                SKU = "RM-0001",
                AvailableQty = 5m,
                OnHandQty = 5m,
                ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(1)),
                LastUpdated = DateTime.UtcNow
            });
            await session.SaveChangesAsync();
        }

        await using var db = CreateDbContext();
        var task = await CreateTaskAsync(db, "ORD-4", 1, 5m);
        var controller = CreateController(db);

        var result = await controller.GetLocationSuggestionsAsync(task.TaskId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<PickingController.PickLocationSuggestionResponse>().Subject;

        payload.Locations.Should().HaveCount(2);
        payload.Locations[0].LocationCode.Should().Be("A-03");
        payload.Locations[1].LocationCode.Should().Be("A-02");
    }

    private WarehouseDbContext CreateDbContext()
        => new(_dbOptions!, new StaticCurrentUserService("picker"));

    private PickingController CreateController(WarehouseDbContext db)
        => new(db, _store!, new StaticCurrentUserService("picker"))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

    private async Task SeedBaseDataAsync()
    {
        await using var db = CreateDbContext();
        db.ItemCategories.Add(new ItemCategory { Id = 1, Code = "RAW", Name = "Raw" });
        db.UnitOfMeasures.Add(new UnitOfMeasure { Code = "PCS", Name = "Pieces", Type = "Piece" });

        db.Items.Add(new Item
        {
            Id = 1,
            InternalSKU = "RM-0001",
            Name = "Bolt",
            CategoryId = 1,
            BaseUoM = "PCS",
            Status = "Active",
            PrimaryBarcode = "BAR-ITEM-1"
        });

        db.ItemBarcodes.Add(new ItemBarcode
        {
            ItemId = 1,
            Barcode = "BAR-ITEM-1",
            BarcodeType = "Code128",
            IsPrimary = true
        });

        db.Locations.AddRange(
            new Location { Id = 1, Code = "SHIPPING", Barcode = "LOC-SHIP", Type = "Zone", IsVirtual = true, Status = "Active" },
            new Location { Id = 2, Code = "A-01", Barcode = "LOC-A01", Type = "Bin", IsVirtual = false, Status = "Active" },
            new Location { Id = 3, Code = "A-02", Barcode = "LOC-A02", Type = "Bin", IsVirtual = false, Status = "Active" },
            new Location { Id = 4, Code = "A-03", Barcode = "LOC-A03", Type = "Bin", IsVirtual = false, Status = "Active" });

        await db.SaveChangesAsync();
    }

    private async Task SeedAvailableStockAsync(decimal qty)
    {
        await using var session = _store!.LightweightSession();
        session.Store(new AvailableStockView
        {
            Id = AvailableStockView.ComputeId("WH1", "A-01", "RM-0001"),
            WarehouseId = "WH1",
            Location = "A-01",
            SKU = "RM-0001",
            AvailableQty = qty,
            OnHandQty = qty,
            LastUpdated = DateTime.UtcNow
        });
        await session.SaveChangesAsync();
    }

    private static async Task<PickTask> CreateTaskAsync(WarehouseDbContext db, string orderId, int itemId, decimal qty)
    {
        var task = new PickTask
        {
            TaskId = Guid.NewGuid(),
            OrderId = orderId,
            ItemId = itemId,
            Qty = qty,
            Status = "Pending"
        };

        db.PickTasks.Add(task);
        await db.SaveChangesAsync();
        return task;
    }

    private sealed class StaticCurrentUserService : ICurrentUserService
    {
        private readonly string _user;

        public StaticCurrentUserService(string user)
        {
            _user = user;
        }

        public string GetCurrentUserId() => _user;
    }
}
