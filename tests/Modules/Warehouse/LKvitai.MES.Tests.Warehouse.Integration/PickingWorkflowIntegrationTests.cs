using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using Microsoft.Extensions.Logging.Abstractions;
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
            .UseNpgsql(_postgres.GetConnectionString(), o => o.EnableRetryOnFailure())
            .Options;

        await using (var db = CreateDbContext())
        {
            await db.Database.EnsureCreatedAsync();
            // EnsureCreated omits the SQL default supplied by AddSalesOrderRowVersion.
            await db.Database.ExecuteSqlRawAsync("""ALTER TABLE public.sales_orders ALTER COLUMN "RowVersion" SET DEFAULT decode('', 'hex')""");
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
    public async Task CreatePickTask_ShouldPersistPendingTask()
    {
        DockerRequirement.EnsureEnabled();

        await SeedBaseDataAsync();

        await using var db = CreateDbContext();
        var controller = CreateController(db);

        var result = await controller.CreateTaskAsync(new PickingController.CreatePickTaskRequest("ORD-1", 1, 10m, null));

        var created = result.Should().BeOfType<CreatedResult>().Subject;
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
    public async Task CompletePickTask_ForOutboundOrder_ShouldMarkOrderPicked()
    {
        DockerRequirement.EnsureEnabled();

        await SeedBaseDataAsync();
        await SeedAvailableStockAsync(5m);

        await using var db = CreateDbContext();
        var order = new OutboundOrder { OrderNumber = "OUT-PICK-1" };
        order.MarkAllocated(Guid.NewGuid()).IsSuccess.Should().BeTrue();
        order.StartPicking().IsSuccess.Should().BeTrue();
        order.Lines.Add(new OutboundOrderLine { ItemId = 1, Qty = 5m });
        db.OutboundOrders.Add(order);
        await db.SaveChangesAsync();

        var task = await CreateTaskAsync(db, order.OrderNumber, 1, 5m);
        var controller = CreateController(db);
        var result = await controller.CompleteTaskAsync(task.TaskId,
            new PickingController.CompletePickTaskRequest(2, 5m, null, "BAR-ITEM-1", "LOC-A01", null));

        result.Should().BeOfType<OkObjectResult>();
        await using var verify = CreateDbContext();
        var saved = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(
            verify.OutboundOrders.Include(x => x.Lines), x => x.Id == order.Id);
        saved.Status.Should().Be(OutboundOrderStatus.Picked);
        saved.Lines.Single().PickedQty.Should().Be(5m);

        var summaryResult = await new OutboundOrdersController(null!, verify).GetSummaryAsync(null, null, null, null);
        var summary = summaryResult.Should().BeOfType<OkObjectResult>().Subject;
        var rows = summary.Value.Should().BeAssignableTo<IEnumerable<OutboundOrdersController.OutboundOrderSummaryResponse>>().Subject;
        rows.Should().ContainSingle(x => x.Id == order.Id && x.Status == "PICKED" && x.ItemCount == 1);

        var shipment = new Shipment { OutboundOrderId = order.Id };
        shipment.Pack(DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        verify.Shipments.Add(shipment);
        await verify.SaveChangesAsync();

        var shipmentsResult = await new ShipmentsController(null!, verify).GetSummaryAsync("PACKED", null, null, null);
        var shipments = shipmentsResult.Should().BeOfType<OkObjectResult>().Subject;
        var shipmentRows = shipments.Value.Should().BeAssignableTo<IEnumerable<ShipmentsController.ShipmentSummaryResponse>>().Subject;
        shipmentRows.Should().ContainSingle(x => x.Id == shipment.Id && x.OutboundOrderNumber == order.OrderNumber);
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

    [SkippableFact]
    public async Task PartialPick_Pack_Dispatch_PersistContentsAndOnlyRealStockMovements()
    {
        DockerRequirement.EnsureEnabled();
        await SeedBaseDataAsync();
        await SeedAvailableStockAsync(5);
        await using var db = CreateDbContext();
        var order = await NewSalesOrderAsync(db);
        var task = await CreateTaskAsync(db, order.OrderNumber, 1, 5);
        var controller = CreateController(db);
        (await controller.CompleteTaskAsync(task.TaskId,
            new PickingController.CompletePickTaskRequest(2, 2, null, "BAR-ITEM-1", "LOC-A01", "QA-partial")))
            .Should().BeOfType<OkObjectResult>();
        var remainder = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(
            db.PickTasks.Where(x => x.Status == "Pending"));
        remainder.Qty.Should().Be(3);
        (await controller.CompleteTaskAsync(remainder.TaskId,
            new PickingController.CompletePickTaskRequest(2, 3, null, "BAR-ITEM-1", "LOC-A01", "QA-remainder")))
            .Should().BeOfType<OkObjectResult>();
        var pack = new PackOrderCommandHandler(db, new QuietBus(), new StaticCurrentUserService("QA-C2"), _store!);
        (await pack.Handle(new PackOrderCommand { OutboundOrderId = order.Id,
            ScannedItems = [new() { Barcode = "BAR-ITEM-1", Qty = 5 }] }, default)).IsSuccess.Should().BeTrue();
        await using var query = _store!.QuerySession();
        (await query.Events.FetchStreamAsync($"outbound-pack:{order.Id:N}")).Should().ContainSingle()
            .Which.Data.Should().BeOfType<ShipmentPackedEvent>();
        (await query.Events.FetchStreamAsync($"pick-task:{task.TaskId:N}")).Single().Data
            .Should().BeOfType<PickCompletedEvent>().Which.PickedQty.Should().Be(2);
        (await query.Events.FetchStreamAsync($"pick-task:{remainder.TaskId:N}")).Single().Data
            .Should().BeOfType<PickCompletedEvent>().Which.PickedQty.Should().Be(3);
        var hu = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.HandlingUnits.Include(x => x.Lines));
        hu.Lines.Should().ContainSingle().Which.Quantity.Should().Be(5);
        hu.Lines.Single().SKU.Should().Be("RM-0001");
        var shipment = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.Shipments);
        var dispatch = new DispatchShipmentCommandHandler(db, null!, new QuietBus(), new StaticCurrentUserService("QA-C2"),
            new BusinessTelemetryService(), NullLogger<DispatchShipmentCommandHandler>.Instance, _store!);
        var command = new DispatchShipmentCommand { ShipmentId = shipment.Id, Carrier = "OTHER", ManualTrackingNumber = "QA-C2" };
        (await dispatch.Handle(command, default)).IsSuccess.Should().BeTrue();
        (await dispatch.Handle(command, default)).IsSuccess.Should().BeFalse();
        var movements = (await query.Events.FetchStreamAsync(
            LKvitai.MES.Modules.Warehouse.Domain.StockLedgerStreamId.For("WH1", "SHIPPING", "RM-0001")))
            .Select(x => x.Data).OfType<StockMovedEvent>().ToList();
        movements.Should().ContainSingle();
        movements[0].Quantity.Should().Be(5);
        movements[0].FromLocation.Should().Be("SHIPPING");
        movements[0].ToLocation.Should().Be("CUSTOMER");
    }

    [SkippableFact]
    public async Task CancelAfterPartialPick_ReturnsOnlyPickedStockAndCancelsRemainder()
    {
        DockerRequirement.EnsureEnabled();
        await SeedBaseDataAsync();
        await SeedAvailableStockAsync(5);
        await using var db = CreateDbContext();
        var order = await NewSalesOrderAsync(db);
        var task = await CreateTaskAsync(db, order.OrderNumber, 1, 5);
        (await CreateController(db).CompleteTaskAsync(task.TaskId,
            new PickingController.CompletePickTaskRequest(2, 2, null, "BAR-ITEM-1", "LOC-A01", "QA-cancel")))
            .Should().BeOfType<OkObjectResult>();
        var cancel = new CancelSalesOrderCommandHandler(db, new QuietBus(), new StaticCurrentUserService("QA-C2"), _store!);
        var command = new CancelSalesOrderCommand { SalesOrderId = order.SalesOrderId!.Value, Reason = "QA-C2-cancel" };
        (await cancel.Handle(command, default)).IsSuccess.Should().BeTrue();
        (await cancel.Handle(command, default)).IsSuccess.Should().BeFalse();
        await using var verify = CreateDbContext();
        (await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(verify.PickTasks.Where(x => x.Status == "Pending"))).Should().Be(0);
        await using var query = _store!.QuerySession();
        var moves = (await query.Events.FetchStreamAsync(
            LKvitai.MES.Modules.Warehouse.Domain.StockLedgerStreamId.For("WH1", "SHIPPING", "RM-0001")))
            .Select(x => x.Data).OfType<StockMovedEvent>().ToList();
        moves.Should().ContainSingle();
        moves[0].Quantity.Should().Be(2);
        moves[0].ToLocation.Should().Be("RECEIVING");
    }

    [SkippableFact]
    public async Task PickMartenFailure_RollsBackTaskAndOutboundCounters()
    {
        DockerRequirement.EnsureEnabled();
        await SeedBaseDataAsync();
        await SeedAvailableStockAsync(5);
        await using var db = CreateDbContext();
        var order = await NewSalesOrderAsync(db);
        var task = await CreateTaskAsync(db, order.OrderNumber, 1, 5);
        await using (var conn = new Npgsql.NpgsqlConnection(_postgres!.GetConnectionString()))
        {
            await conn.OpenAsync();
            await using var cmd = new Npgsql.NpgsqlCommand("""
                CREATE FUNCTION warehouse_events.qa_fail() RETURNS trigger AS $$
                BEGIN RAISE EXCEPTION 'QA event failure'; END; $$ LANGUAGE plpgsql;
                CREATE TRIGGER qa_fail BEFORE INSERT ON warehouse_events.mt_events
                FOR EACH ROW EXECUTE FUNCTION warehouse_events.qa_fail();
                """, conn);
            await cmd.ExecuteNonQueryAsync();
        }
        var action = async () => await CreateController(db).CompleteTaskAsync(task.TaskId,
            new PickingController.CompletePickTaskRequest(2, 2, null, "BAR-ITEM-1", "LOC-A01", "QA-failure"));
        await action.Should().ThrowAsync<Exception>();
        await using var verify = CreateDbContext();
        (await verify.PickTasks.FindAsync(task.TaskId))!.Status.Should().Be("Pending");
        (await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(verify.OutboundOrderLines)).PickedQty.Should().Be(0);
        (await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(verify.PickTasks)).Should().Be(1);
        await using var query = _store!.QuerySession();
        (await query.Events.FetchStreamAsync($"pick-task:{task.TaskId:N}")).Should().BeEmpty();
    }

    private async Task<OutboundOrder> NewSalesOrderAsync(WarehouseDbContext db)
    {
        var customer = new Customer { CustomerCode = "QA-C2", Name = "QA-C2" };
        var sale = new SalesOrder { OrderNumber = "QA-C2-SALE", Customer = customer };
        sale.Lines.Add(new SalesOrderLine { ItemId = 1, OrderedQty = 5, UnitPrice = 10 });
        sale.Allocate(Guid.NewGuid()).IsSuccess.Should().BeTrue();
        sale.Release().IsSuccess.Should().BeTrue();
        var order = new OutboundOrder { OrderNumber = "QA-C2-OUT", SalesOrder = sale };
        order.MarkAllocated(sale.ReservationId!.Value).IsSuccess.Should().BeTrue();
        order.StartPicking().IsSuccess.Should().BeTrue();
        order.Lines.Add(new OutboundOrderLine { ItemId = 1, Qty = 5 });
        db.OutboundOrders.Add(order);
        db.Locations.AddRange(
            new Location { Id = 5, Code = "CUSTOMER", Barcode = "CUSTOMER", Type = "Zone", IsVirtual = true },
            new Location { Id = 6, Code = "RECEIVING", Barcode = "RECEIVING", Type = "Zone", IsVirtual = true });
        await db.SaveChangesAsync();
        return order;
    }

    private sealed class QuietBus : IEventBus
    {
        public Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class => Task.CompletedTask;
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
