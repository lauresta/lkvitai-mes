using FluentAssertions;
using LKvitai.MES.Api.Controllers;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Projections;
using Marten;
using Marten.Events.Projections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public class ReceivingWorkflowIntegrationTests : IAsyncLifetime
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
    public async Task CreateShipment_ShouldPersistShipmentAndAppendEvent()
    {
        DockerRequirement.EnsureEnabled();

        await SeedBaseDataAsync(requiresLotTracking: false, requiresQc: false);

        await using var db = CreateDbContext();
        var controller = CreateReceivingController(db);

        var result = await controller.CreateShipmentAsync(new ReceivingController.CreateInboundShipmentRequest(
            "PO-100",
            1,
            "PurchaseOrder",
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            [new ReceivingController.CreateInboundShipmentLineRequest(1, 100m)]));

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var payload = created.Value.Should().BeOfType<ReceivingController.ShipmentCreatedResponse>().Subject;

        payload.Id.Should().BeGreaterThan(0);

        var shipment = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(
            db.InboundShipments.Include(x => x.Lines));
        shipment.ReferenceNumber.Should().Be("PO-100");
        shipment.Lines.Should().HaveCount(1);

        await using var session = _store!.QuerySession();
        var events = await session.Events.FetchStreamAsync(ShipmentStreamId(payload.Id));
        events.Select(x => x.Data).Should().Contain(x => x is InboundShipmentCreatedEvent);
    }

    [SkippableFact]
    public async Task ReceiveGoods_LotTrackedWithoutLot_ShouldReturn422()
    {
        DockerRequirement.EnsureEnabled();

        await SeedBaseDataAsync(requiresLotTracking: true, requiresQc: true);

        await using var db = CreateDbContext();
        var shipment = await CreateShipmentAsync(db, "PO-LOT", 1, 1, 50m);
        var lineId = shipment.Lines.Single().Id;

        var controller = CreateReceivingController(db);
        var result = await controller.ReceiveGoodsAsync(
            shipment.Id,
            new ReceivingController.ReceiveShipmentLineRequest(
                lineId,
                10m,
                null,
                null,
                null,
                null));

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
    }

    [SkippableFact]
    public async Task ReceiveGoods_WithLotAndQcItem_ShouldCreateLotAndRouteToQcHold()
    {
        DockerRequirement.EnsureEnabled();

        await SeedBaseDataAsync(requiresLotTracking: true, requiresQc: true);

        await using var db = CreateDbContext();
        var shipment = await CreateShipmentAsync(db, "PO-QC", 1, 1, 80m);
        var lineId = shipment.Lines.Single().Id;

        var controller = CreateReceivingController(db);
        var result = await controller.ReceiveGoodsAsync(
            shipment.Id,
            new ReceivingController.ReceiveShipmentLineRequest(
                lineId,
                20m,
                "LOT-001",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                DateOnly.FromDateTime(DateTime.UtcNow.Date.AddMonths(6)),
                "partial"));

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ReceivingController.ReceiveGoodsResponse>().Subject;

        payload.DestinationLocationCode.Should().Be("QC_HOLD");
        payload.LotId.Should().NotBeNull();

        var lot = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.Lots);
        lot.LotNumber.Should().Be("LOT-001");
    }

    [SkippableFact]
    public async Task QcPass_WithAvailableStock_ShouldAppendEvent()
    {
        DockerRequirement.EnsureEnabled();

        await SeedBaseDataAsync(requiresLotTracking: false, requiresQc: true);

        await using var session = _store!.LightweightSession();
        session.Store(new AvailableStockView
        {
            Id = AvailableStockView.ComputeId("WH1", "QC_HOLD", "RM-0001"),
            WarehouseId = "WH1",
            Location = "QC_HOLD",
            LocationCode = "QC_HOLD",
            SKU = "RM-0001",
            ItemId = 1,
            OnHandQty = 100m,
            AvailableQty = 100m,
            LastUpdated = DateTime.UtcNow
        });
        await session.SaveChangesAsync();

        await using var db = CreateDbContext();
        var controller = CreateQcController(db);

        var result = await controller.PassAsync(
            new QCController.QcActionRequest(1, null, 25m, null, "ok"),
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<QCController.QcActionResponse>();

        await using var query = _store.QuerySession();
        var hasQcPass = await Marten.QueryableExtensions.AnyAsync(
            query.Events.QueryAllRawEvents(),
            x => x.Data is QCPassedEvent);
        hasQcPass.Should().BeTrue();
    }

    [SkippableFact]
    public async Task ReceiveThenQcPass_ShouldMoveStockFromQcHoldToReceiving()
    {
        DockerRequirement.EnsureEnabled();

        await SeedBaseDataAsync(requiresLotTracking: false, requiresQc: true);

        await using var db = CreateDbContext();
        var shipment = await CreateShipmentAsync(db, "PO-QC-PASS", 1, 1, 50m);
        var lineId = shipment.Lines.Single().Id;

        var receivingController = CreateReceivingController(db);
        var receiveResult = await receivingController.ReceiveGoodsAsync(
            shipment.Id,
            new ReceivingController.ReceiveShipmentLineRequest(
                lineId,
                20m,
                null,
                null,
                null,
                "received"));

        var receiveOk = receiveResult.Should().BeOfType<OkObjectResult>().Subject;
        var receivePayload = receiveOk.Value.Should().BeOfType<ReceivingController.ReceiveGoodsResponse>().Subject;
        receivePayload.DestinationLocationCode.Should().Be("QC_HOLD");

        await RunDaemonAsync();

        var qcController = CreateQcController(db);
        var qcResult = await qcController.PassAsync(
            new QCController.QcActionRequest(1, null, 12m, null, "qc pass"),
            CancellationToken.None);

        var qcOk = qcResult.Should().BeOfType<OkObjectResult>().Subject;
        var qcPayload = qcOk.Value.Should().BeOfType<QCController.QcActionResponse>().Subject;
        qcPayload.DestinationLocationCode.Should().Be("RECEIVING");

        await RunDaemonAsync();

        await using var query = _store!.QuerySession();
        var qcHold = await Marten.QueryableExtensions.SingleOrDefaultAsync(
            query.Query<AvailableStockView>()
                .Where(x => x.WarehouseId == "WH1" && x.Location == "QC_HOLD" && x.SKU == "RM-0001"),
            CancellationToken.None);
        var rawEvents = await Marten.QueryableExtensions.ToListAsync(query.Events.QueryAllRawEvents(), CancellationToken.None);
        var hasQcPass = rawEvents
            .Select(x => x.Data)
            .OfType<QCPassedEvent>()
            .Any(x =>
                x.SKU == "RM-0001" &&
                x.FromLocation == "QC_HOLD" &&
                x.ToLocation == "RECEIVING" &&
                x.Qty == 12m);

        qcHold.Should().NotBeNull();
        qcHold!.OnHandQty.Should().Be(8m);
        qcHold.AvailableQty.Should().Be(8m);
        hasQcPass.Should().BeTrue();
    }

    [SkippableFact]
    public async Task ReceiveThenQcFail_ShouldMoveStockFromQcHoldToQuarantine()
    {
        DockerRequirement.EnsureEnabled();

        await SeedBaseDataAsync(requiresLotTracking: false, requiresQc: true);

        await using var db = CreateDbContext();
        var shipment = await CreateShipmentAsync(db, "PO-QC-FAIL", 1, 1, 50m);
        var lineId = shipment.Lines.Single().Id;

        var receivingController = CreateReceivingController(db);
        var receiveResult = await receivingController.ReceiveGoodsAsync(
            shipment.Id,
            new ReceivingController.ReceiveShipmentLineRequest(
                lineId,
                20m,
                null,
                null,
                null,
                "received"));

        receiveResult.Should().BeOfType<OkObjectResult>();

        await RunDaemonAsync();

        var qcController = CreateQcController(db);
        var qcResult = await qcController.FailAsync(
            new QCController.QcActionRequest(1, null, 7m, "DAMAGE", "qc fail"),
            CancellationToken.None);

        var qcOk = qcResult.Should().BeOfType<OkObjectResult>().Subject;
        var qcPayload = qcOk.Value.Should().BeOfType<QCController.QcActionResponse>().Subject;
        qcPayload.DestinationLocationCode.Should().Be("QUARANTINE");

        await RunDaemonAsync();

        await using var query = _store!.QuerySession();
        var qcHold = await Marten.QueryableExtensions.SingleOrDefaultAsync(
            query.Query<AvailableStockView>()
                .Where(x => x.WarehouseId == "WH1" && x.Location == "QC_HOLD" && x.SKU == "RM-0001"),
            CancellationToken.None);
        var rawEvents = await Marten.QueryableExtensions.ToListAsync(query.Events.QueryAllRawEvents(), CancellationToken.None);
        var hasQcFail = rawEvents
            .Select(x => x.Data)
            .OfType<QCFailedEvent>()
            .Any(x =>
                x.SKU == "RM-0001" &&
                x.FromLocation == "QC_HOLD" &&
                x.ToLocation == "QUARANTINE" &&
                x.Qty == 7m);

        qcHold.Should().NotBeNull();
        qcHold!.OnHandQty.Should().Be(13m);
        qcHold.AvailableQty.Should().Be(13m);
        hasQcFail.Should().BeTrue();
    }

    private WarehouseDbContext CreateDbContext()
        => new(_dbOptions!, new StaticCurrentUserService("tester"));

    private ReceivingController CreateReceivingController(WarehouseDbContext db)
        => new(db, _store!, new StaticCurrentUserService("tester"))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

    private QCController CreateQcController(WarehouseDbContext db)
        => new(db, _store!, new StaticCurrentUserService("qc-tester"), new StubSignatureService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

    private async Task SeedBaseDataAsync(bool requiresLotTracking, bool requiresQc)
    {
        await using var db = CreateDbContext();
        db.ItemCategories.Add(new ItemCategory { Id = 1, Code = "RAW", Name = "Raw" });
        db.UnitOfMeasures.Add(new UnitOfMeasure { Code = "PCS", Name = "Pieces", Type = "Piece" });
        db.Suppliers.Add(new Supplier { Id = 1, Code = "SUP-1", Name = "Supplier" });
        db.Items.Add(new Item
        {
            Id = 1,
            InternalSKU = "RM-0001",
            Name = "Bolt",
            CategoryId = 1,
            BaseUoM = "PCS",
            RequiresLotTracking = requiresLotTracking,
            RequiresQC = requiresQc,
            Status = "Active"
        });

        db.Locations.AddRange(
            new Location { Id = 1, Code = "RECEIVING", Barcode = "VIRTUAL-RCV", Type = "Zone", IsVirtual = true, Status = "Active" },
            new Location { Id = 2, Code = "QC_HOLD", Barcode = "VIRTUAL-QC", Type = "Zone", IsVirtual = true, Status = "Active" },
            new Location { Id = 3, Code = "QUARANTINE", Barcode = "VIRTUAL-QTN", Type = "Zone", IsVirtual = true, Status = "Active" });

        db.AdjustmentReasonCodes.Add(new AdjustmentReasonCode { Code = "DAMAGE", Name = "Damage", IsActive = true });

        await db.SaveChangesAsync();
    }

    private static async Task<InboundShipment> CreateShipmentAsync(
        WarehouseDbContext db,
        string reference,
        int supplierId,
        int itemId,
        decimal expectedQty)
    {
        var shipment = new InboundShipment
        {
            ReferenceNumber = reference,
            SupplierId = supplierId,
            Status = "Draft",
            Lines =
            {
                new InboundShipmentLine
                {
                    ItemId = itemId,
                    BaseUoM = "PCS",
                    ExpectedQty = expectedQty,
                    ReceivedQty = 0m
                }
            }
        };

        db.InboundShipments.Add(shipment);
        await db.SaveChangesAsync();
        return shipment;
    }

    private static string ShipmentStreamId(int shipmentId) => $"inbound-shipment:{shipmentId}";

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

    private sealed class StubSignatureService : IElectronicSignatureService
    {
        public Task<ElectronicSignature> CaptureAsync(CaptureSignatureCommand command, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ElectronicSignature
            {
                Id = 1,
                Action = command.Action,
                ResourceId = command.ResourceId,
                SignatureText = command.SignatureText,
                Meaning = command.Meaning,
                UserId = command.UserId,
                Timestamp = DateTimeOffset.UtcNow,
                IpAddress = command.IpAddress,
                PreviousHash = "GENESIS",
                CurrentHash = "HASH"
            });
        }

        public Task<IReadOnlyList<ElectronicSignature>> GetByResourceAsync(string resourceId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ElectronicSignature>>(Array.Empty<ElectronicSignature>());

        public Task<VerifyHashChainResponse> VerifyHashChainAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new VerifyHashChainResponse(true, 0));
    }
}
