using System.Text;
using FluentAssertions;
using LKvitai.MES.Api.Controllers;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Application.Services;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.Projections;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public class AdjustmentsWorkflowIntegrationTests : IAsyncLifetime
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

        await using (var db = CreateDbContext())
        {
            await db.Database.EnsureCreatedAsync();
        }

        _store = DocumentStore.For(opts =>
        {
            opts.Connection(_postgres.GetConnectionString());
            opts.DatabaseSchemaName = "warehouse_events";
            opts.Events.DatabaseSchemaName = "warehouse_events";
            opts.Events.StreamIdentity = Marten.Events.StreamIdentity.AsString;
            opts.RegisterProjections();
        });
        await _store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
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
    public async Task CreateAdjustment_ShouldAppendStockAdjustedEvent()
    {
        DockerRequirement.EnsureEnabled();

        await SeedBaseDataAsync();
        await SeedStockAsync(location: "A-01", qty: 100m);

        await using var db = CreateDbContext();
        var controller = CreateController(db);

        var result = await controller.CreateAsync(
            new AdjustmentsController.CreateAdjustmentRequest(
                ItemId: 1,
                LocationId: 1,
                QtyDelta: 15m,
                ReasonCode: "INVENTORY",
                Notes: "count correction",
                LotId: null));

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<AdjustmentsController.CreateAdjustmentResponse>().Subject;
        payload.Warning.Should().BeNull();
        payload.QtyDelta.Should().Be(15m);
        payload.ReasonCode.Should().Be("INVENTORY");

        await using var query = _store!.QuerySession();
        var stream = await query.Events.FetchStreamAsync($"stock-adjustment:{payload.AdjustmentId:N}");
        stream.Select(x => x.Data).Should().ContainSingle(x => x is StockAdjustedEvent);
    }

    [SkippableFact]
    public async Task CreateAdjustment_NegativeStock_ShouldReturnWarningAndStillAppendEvent()
    {
        DockerRequirement.EnsureEnabled();

        await SeedBaseDataAsync();
        await SeedStockAsync(location: "A-01", qty: 5m);

        await using var db = CreateDbContext();
        var controller = CreateController(db);

        var result = await controller.CreateAsync(
            new AdjustmentsController.CreateAdjustmentRequest(
                ItemId: 1,
                LocationId: 1,
                QtyDelta: -10m,
                ReasonCode: "DAMAGE",
                Notes: "broken during handling",
                LotId: null));

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<AdjustmentsController.CreateAdjustmentResponse>().Subject;
        payload.Warning.Should().NotBeNullOrWhiteSpace();
        payload.Warning.Should().Contain("Negative stock warning");

        await using var query = _store!.QuerySession();
        var stream = await query.Events.FetchStreamAsync($"stock-adjustment:{payload.AdjustmentId:N}");
        stream.Select(x => x.Data).Should().ContainSingle(
            x => x is StockAdjustedEvent && ((StockAdjustedEvent)x).QtyDelta == -10m);
    }

    [SkippableFact]
    public async Task CreateAdjustment_ShouldUpdateProjectionAndBeVisibleInHistory()
    {
        DockerRequirement.EnsureEnabled();

        await SeedBaseDataAsync();
        await SeedStockAsync(location: "A-01", qty: 20m);

        await using var db = CreateDbContext();
        var controller = CreateController(db);

        var createResult = await controller.CreateAsync(
            new AdjustmentsController.CreateAdjustmentRequest(
                ItemId: 1,
                LocationId: 1,
                QtyDelta: 5m,
                ReasonCode: "INVENTORY",
                Notes: "cycle count",
                LotId: null));

        var createOk = createResult.Should().BeOfType<OkObjectResult>().Subject;
        var created = createOk.Value.Should().BeOfType<AdjustmentsController.CreateAdjustmentResponse>().Subject;
        created.UserId.Should().Be("manager-1");

        await RunDaemonAsync();

        var historyResult = await controller.GetAsync(itemId: 1, locationId: 1, pageSize: 20);
        var historyOk = historyResult.Should().BeOfType<OkObjectResult>().Subject;
        var history = historyOk.Value.Should().BeOfType<AdjustmentsController.PagedResponse<AdjustmentsController.AdjustmentHistoryItemDto>>().Subject;

        history.TotalCount.Should().BeGreaterThan(0);
        history.Items.Should().ContainSingle(x =>
            x.AdjustmentId == created.AdjustmentId &&
            x.ReasonCode == "INVENTORY" &&
            x.UserId == "manager-1" &&
            x.Notes == "cycle count");

        await using var query = _store!.QuerySession();
        var stock = await Marten.QueryableExtensions.SingleOrDefaultAsync(
            query.Query<AvailableStockView>()
                .Where(x => x.WarehouseId == "WH1" && x.Location == "A-01" && x.SKU == "RM-0001"),
            CancellationToken.None);

        stock.Should().NotBeNull();
        stock!.OnHandQty.Should().Be(25m);
        stock.AvailableQty.Should().Be(25m);
    }

    [SkippableFact]
    public async Task CreateAdjustment_WithoutReasonCode_ShouldReturn400()
    {
        DockerRequirement.EnsureEnabled();

        await SeedBaseDataAsync();

        await using var db = CreateDbContext();
        var controller = CreateController(db);

        var result = await controller.CreateAsync(
            new AdjustmentsController.CreateAdjustmentRequest(
                ItemId: 1,
                LocationId: 1,
                QtyDelta: 1m,
                ReasonCode: string.Empty,
                Notes: null,
                LotId: null));

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [SkippableFact]
    public async Task GetAdjustments_FilterAndCsv_ShouldReturnExpectedRows()
    {
        DockerRequirement.EnsureEnabled();

        await SeedBaseDataAsync();

        await using (var session = _store!.LightweightSession())
        {
            session.Store(new AdjustmentHistoryView
            {
                Id = Guid.NewGuid().ToString("N"),
                AdjustmentId = Guid.NewGuid(),
                ItemId = 1,
                SKU = "RM-0001",
                ItemName = "Bolt",
                LocationId = 1,
                Location = "A-01",
                LocationCode = "A-01",
                QtyDelta = -5m,
                ReasonCode = "DAMAGE",
                Notes = "damaged",
                UserId = "manager-1",
                UserName = "Manager One",
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-10)
            });

            session.Store(new AdjustmentHistoryView
            {
                Id = Guid.NewGuid().ToString("N"),
                AdjustmentId = Guid.NewGuid(),
                ItemId = 1,
                SKU = "RM-0001",
                ItemName = "Bolt",
                LocationId = 2,
                Location = "B-01",
                LocationCode = "B-01",
                QtyDelta = 8m,
                ReasonCode = "INVENTORY",
                Notes = "count up",
                UserId = "manager-1",
                UserName = "Manager One",
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-5)
            });

            await session.SaveChangesAsync();
        }

        await using var db = CreateDbContext();
        var controller = CreateController(db);

        var filtered = await controller.GetAsync(reasonCode: "DAMAGE", pageSize: 50);
        var ok = filtered.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<AdjustmentsController.PagedResponse<AdjustmentsController.AdjustmentHistoryItemDto>>().Subject;
        payload.TotalCount.Should().Be(1);
        payload.Items.Should().ContainSingle();
        payload.Items[0].ReasonCode.Should().Be("DAMAGE");

        var csvResult = await controller.GetAsync(exportCsv: true, reasonCode: "DAMAGE");
        var file = csvResult.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("text/csv");
        Encoding.UTF8.GetString(file.FileContents).Should().Contain("AdjustmentId,ItemId,SKU,LocationId,LocationCode,QtyDelta,ReasonCode,UserId,Timestamp,Notes");
        Encoding.UTF8.GetString(file.FileContents).Should().Contain("DAMAGE");
        Encoding.UTF8.GetString(file.FileContents).Should().NotContain("INVENTORY");
    }

    private WarehouseDbContext CreateDbContext()
        => new(_dbOptions!, new StaticCurrentUserService("manager-1"));

    private AdjustmentsController CreateController(WarehouseDbContext db)
        => new(
            db,
            _store!,
            new StaticCurrentUserService("manager-1"),
            new ReasonCodeService(db, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance.CreateLogger<ReasonCodeService>()))
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
            Status = "Active"
        });

        db.Locations.AddRange(
            new Location { Id = 1, Code = "A-01", Barcode = "LOC-A01", Type = "Bin", IsVirtual = false, Status = "Active" },
            new Location { Id = 2, Code = "B-01", Barcode = "LOC-B01", Type = "Bin", IsVirtual = false, Status = "Active" });

        db.AdjustmentReasonCodes.AddRange(
            new AdjustmentReasonCode { Code = "DAMAGE", Name = "Damage", IsActive = true },
            new AdjustmentReasonCode { Code = "INVENTORY", Name = "Inventory Count", IsActive = true });

        await db.SaveChangesAsync();
    }

    private async Task SeedStockAsync(string location, decimal qty)
    {
        await using var session = _store!.LightweightSession();
        session.Store(new AvailableStockView
        {
            Id = AvailableStockView.ComputeId("WH1", location, "RM-0001"),
            WarehouseId = "WH1",
            Location = location,
            LocationCode = location,
            SKU = "RM-0001",
            ItemId = 1,
            OnHandQty = qty,
            AvailableQty = qty,
            LastUpdated = DateTime.UtcNow
        });

        await session.SaveChangesAsync();
    }

    private async Task RunDaemonAsync()
    {
        using var daemon = await _store!.BuildProjectionDaemonAsync();
        await daemon.StartAllAsync();
        await daemon.WaitForNonStaleData(TimeSpan.FromSeconds(15));
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
