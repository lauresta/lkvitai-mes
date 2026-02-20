using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Domain;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Projections;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public class PutawayWorkflowIntegrationTests : IAsyncLifetime
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
            .WithImage("postgres:16-alpine")
            .Build();

        await _postgres.StartAsync();

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
    public async Task PutawayTasks_ShouldReturnRowsFromReceivingLocation()
    {
        DockerRequirement.EnsureEnabled();

        await SeedBaseDataAsync();
        await SeedStockProjectionAsync(receivingQty: 40m, destinationQty: 0m);

        await using var db = CreateDbContext();
        var controller = CreatePutawayController(db);

        var result = await controller.GetTasksAsync();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<PutawayController.PagedResponse<PutawayController.PutawayTaskDto>>().Subject;

        payload.TotalCount.Should().Be(1);
        payload.Items.Should().ContainSingle();
        payload.Items[0].InternalSKU.Should().Be("RM-0001");
    }

    [SkippableFact]
    public async Task Putaway_WhenInsufficientStock_ShouldReturn422()
    {
        DockerRequirement.EnsureEnabled();

        await SeedBaseDataAsync();
        await SeedStockProjectionAsync(receivingQty: 10m, destinationQty: 0m);

        await using var db = CreateDbContext();
        var controller = CreatePutawayController(db);

        var result = await controller.PutawayAsync(new PutawayController.PutawayRequest(1, 20m, 1, 2, null, null));

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [SkippableFact]
    public async Task Putaway_WhenCapacityExceeds80Percent_ShouldReturnWarningAndEmitEvent()
    {
        DockerRequirement.EnsureEnabled();

        await SeedBaseDataAsync();
        await SeedStockProjectionAsync(receivingQty: 20m, destinationQty: 75m);

        await using var db = CreateDbContext();
        var controller = CreatePutawayController(db);

        var result = await controller.PutawayAsync(new PutawayController.PutawayRequest(1, 10m, 1, 2, null, "putaway"));

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<PutawayController.PutawayResponse>().Subject;
        payload.Warning.Should().NotBeNullOrWhiteSpace();

        await using var query = _store!.QuerySession();
        var streamId = StockLedgerStreamId.For("WH1", "RECEIVING", "RM-0001");
        var events = await query.Events.FetchStreamAsync(streamId);
        events.Select(x => x.Data).Should().Contain(x => x is StockMovedEvent);
    }

    [SkippableFact]
    public async Task BarcodeLookup_BlockedLocation_ShouldReturn422()
    {
        DockerRequirement.EnsureEnabled();

        await SeedBaseDataAsync();

        await using var db = CreateDbContext();
        db.Locations.Add(new Location
        {
            Code = "BLOCK-1",
            Barcode = "BAR-BLOCK",
            Type = "Bin",
            IsVirtual = false,
            Status = "Blocked"
        });
        await db.SaveChangesAsync();

        var controller = new BarcodesController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.LookupAsync("BAR-BLOCK", "location");

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    private WarehouseDbContext CreateDbContext()
        => new(_dbOptions!, new StaticCurrentUserService("tester"));

    private PutawayController CreatePutawayController(WarehouseDbContext db)
        => new(db, _store!, new StaticCurrentUserService("operator"))
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
            Weight = 1m,
            Volume = 0.1m,
            Status = "Active"
        });

        db.Locations.AddRange(
            new Location { Id = 1, Code = "RECEIVING", Barcode = "VIRTUAL-RCV", Type = "Zone", IsVirtual = true, Status = "Active" },
            new Location { Id = 2, Code = "A-01", Barcode = "A-01", Type = "Bin", IsVirtual = false, Status = "Active", MaxWeight = 100m, MaxVolume = 100m });

        await db.SaveChangesAsync();
    }

    private async Task SeedStockProjectionAsync(decimal receivingQty, decimal destinationQty)
    {
        await using var session = _store!.LightweightSession();
        session.Store(new AvailableStockView
        {
            Id = AvailableStockView.ComputeId("WH1", "RECEIVING", "RM-0001"),
            WarehouseId = "WH1",
            Location = "RECEIVING",
            LocationCode = "RECEIVING",
            SKU = "RM-0001",
            ItemId = 1,
            OnHandQty = receivingQty,
            AvailableQty = receivingQty,
            LastUpdated = DateTime.UtcNow
        });

        if (destinationQty > 0)
        {
            session.Store(new AvailableStockView
            {
                Id = AvailableStockView.ComputeId("WH1", "A-01", "RM-0001"),
                WarehouseId = "WH1",
                Location = "A-01",
                LocationCode = "A-01",
                SKU = "RM-0001",
                ItemId = 1,
                OnHandQty = destinationQty,
                AvailableQty = destinationQty,
                LastUpdated = DateTime.UtcNow
            });
        }

        await session.SaveChangesAsync();
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
