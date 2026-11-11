using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Integration;

public partial class PickingWorkflowIntegrationTests
{
    private DatabaseWarehouseStore DurableStore(WarehouseDbContext db) => new(db, _store!,
        new PostgresBalanceGuardLockFactory(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["ConnectionStrings:WarehouseDb"] = _postgres!.GetConnectionString() }).Build()));

    [SkippableTheory]
    [InlineData("RESTOCK", false)]
    [InlineData("SCRAP", false)]
    [InlineData("RESTOCK", true)]
    public async Task Rma_PersistsReceiptAndDispositionInMarten_IdempotentlyAndAtomically(string disposition, bool failInspection)
    {
        DockerRequirement.EnsureEnabled();
        await SeedBaseDataAsync();
        await using var db = CreateDbContext();
        db.Locations.Add(new Location { Id = 5, Code = "QC_HOLD", Barcode = "QC_HOLD", Type = "Zone", IsVirtual = true });
        await db.SaveChangesAsync();
        var order = await Draft(db);
        var service = DurableStore(db);
        var rma = service.CreateRma(new(order.Id, "QA-C6-return", [new(1, 2, "QA-C6", order.Lines.Single().Id)], "QA-C6"));
        var receipt = await service.ReceiveRmaAsync(rma.Id, "QA-C6");
        receipt!.Status.Should().Be(RmaStatus.Received);
        await service.ReceiveRmaAsync(rma.Id, "QA-C6");
        if (failInspection)
            await db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION public.qa_reject_rma_move() RETURNS trigger AS $$ BEGIN
                  IF NEW.data->>'FromLocation' = 'QC_HOLD' THEN RAISE EXCEPTION 'QA-C6 movement failure'; END IF;
                  RETURN NEW; END; $$ LANGUAGE plpgsql;
                CREATE TRIGGER qa_reject_rma_move BEFORE INSERT ON warehouse_events.mt_events
                FOR EACH ROW EXECUTE FUNCTION public.qa_reject_rma_move();
                """);
        var inspect = async () => await service.InspectRmaAsync(rma.Id, disposition, 12.50m, "QA-C6");
        if (failInspection) await inspect.Should().ThrowAsync<Exception>();
        else { await inspect(); await inspect(); }
        await using var fresh = CreateDbContext();
        var saved = DurableStore(fresh).GetRmas().Single();
        saved.Lines.Single().SalesOrderLineId.Should().Be(order.Lines.Single().Id);
        saved.Status.Should().Be(failInspection ? RmaStatus.Received : disposition == "SCRAP" ? RmaStatus.Scrapped : RmaStatus.Restocked);
        saved.CreditAmount.Should().Be(failInspection ? null : 12.50m);
        await using var query = _store!.QuerySession();
        var receipts = await CommittedEventData.ReadAsync<StockAdjustedEvent>(query, "RM-0001", default);
        var moves = await CommittedEventData.ReadAsync<StockMovedEvent>(query, "RM-0001", default);
        receipts.Should().ContainSingle();
        moves.Should().HaveCount(failInspection ? 0 : 1);
        var all = receipts.Cast<object>().Concat(moves);
        StockEventQuantity.CalculateAtLocation(all, "RM-0001", "QC_HOLD").Should().Be(failInspection ? 2 : 0);
        StockEventQuantity.CalculateAtLocation(all, "RM-0001", disposition == "SCRAP" ? "SCRAP" : "RECEIVING").Should().Be(failInspection ? 0 : 2);
        var excess = () => DurableStore(fresh).CreateRma(new(order.Id, "QA-C6-excess", [new(1, 4, null)], "QA-C6"));
        excess.Should().Throw<WarehouseOperationValidationException>();
    }

    [SkippableFact]
    public async Task Rma_ReceiptFailureDoesNotPersistReceivedStateOrStock()
    {
        DockerRequirement.EnsureEnabled(); await SeedBaseDataAsync();
        await using var db = CreateDbContext();
        db.Locations.Add(new Location { Id = 5, Code = "QC_HOLD", Barcode = "QC_HOLD", Type = "Zone", IsVirtual = true });
        await db.SaveChangesAsync();
        var order = await Draft(db);
        var service = DurableStore(db);
        var rma = service.CreateRma(new(order.Id, "QA-C6-rollback", [new(1, 1, null)], "QA-C6"));
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION public.qa_reject_rma_receipt() RETURNS trigger AS $$ BEGIN
              IF NEW.data->>'ReasonCode' = 'RMA' THEN RAISE EXCEPTION 'QA-C6 receipt failure'; END IF;
              RETURN NEW; END; $$ LANGUAGE plpgsql;
            CREATE TRIGGER qa_reject_rma_receipt BEFORE INSERT ON warehouse_events.mt_events
            FOR EACH ROW EXECUTE FUNCTION public.qa_reject_rma_receipt();
            """);
        var receive = async () => await service.ReceiveRmaAsync(rma.Id, "QA-C6");
        await receive.Should().ThrowAsync<Exception>();
        await using var fresh = CreateDbContext();
        DurableStore(fresh).GetRmas().Single().Status.Should().Be(RmaStatus.PendingReceipt);
        await using var query = _store!.QuerySession();
        (await CommittedEventData.ReadAsync<StockAdjustedEvent>(query, "RM-0001", default)).Should().BeEmpty();
    }

    [SkippableFact]
    public async Task Wave_UsesRealReleasedTasks_AndOrdinaryPickingDeterminesProgress()
    {
        DockerRequirement.EnsureEnabled(); await SeedBaseDataAsync(); await SeedAvailableStockAsync(5);
        await using var db = CreateDbContext();
        var order = await Draft(db);
        using var headersStore = Marten.DocumentStore.For(opts => {
            opts.Connection(_postgres!.GetConnectionString());
            opts.DatabaseSchemaName = "warehouse_events";
            opts.Events.DatabaseSchemaName = "warehouse_events";
            opts.Events.StreamIdentity = Marten.Events.StreamIdentity.AsString;
            opts.Events.MetadataConfig.HeadersEnabled = true;
            typeof(MartenConfiguration).GetMethod("RegisterMartenEventTypes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .Invoke(null, new object[] { opts });
        });
        await headersStore.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
        var service = new DatabaseWarehouseStore(db, headersStore,
            new PostgresBalanceGuardLockFactory(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
                ["ConnectionStrings:WarehouseDb"] = _postgres!.GetConnectionString() }).Build()));
        var missing = () => service.CreateWave([Guid.NewGuid()], "QA-C6");
        missing.Should().Throw<WarehouseOperationValidationException>();
        (await Reservations(db).AllocateAsync(order, () => order.Allocate(order.Id), default)).IsSuccess.Should().BeTrue();
        (await new ReleaseSalesOrderCommandHandler(db, new QuietBus()).Handle(new ReleaseSalesOrderCommand { SalesOrderId = order.Id }, default)).IsSuccess.Should().BeTrue();
        var task = await db.PickTasks.SingleAsync();
        // Deployed events have JSONB headers; reading only payloads must work without dynamic JSON opt-in.
        await db.Database.ExecuteSqlRawAsync("UPDATE warehouse_events.mt_events SET headers = jsonb_build_object()");
        var wave = service.CreateWave([order.OutboundOrderId!.Value], "QA-C6");
        wave.PickList.Single().TaskId.Should().Be(task.TaskId);
        wave.PickList.Single().Location.Should().Be("A-01");
        wave.PickList.Single().Qty.Should().Be(5);
        service.StartWave(wave.Id);
        var duplicate = () => service.CreateWave([order.OutboundOrderId.Value], "QA-C6");
        duplicate.Should().Throw<WarehouseOperationValidationException>();
        // The ordinary picking endpoint writes both the real task and movement.
        var result = await CreateController(db).CompleteTaskAsync(task.TaskId,
            new LKvitai.MES.Modules.Warehouse.Api.Controllers.PickingController.CompletePickTaskRequest(2, 5m, null, "BAR-ITEM-1", "LOC-A01", "QA-C6-pick"), default);
        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.OkObjectResult>();
        await using var fresh = CreateDbContext();
        var saved = DurableStore(fresh).GetWave(wave.Id)!;
        saved.Status.Should().Be(WaveStatus.Completed);
        saved.CompletedLines.Should().Be(1);
        saved.PickList.Single().Status.Should().Be("Completed");
    }

    [SkippableFact]
    public async Task SerialQcLabelsAndReconciliation_SurviveNewDbContext()
    {
        DockerRequirement.EnsureEnabled(); await SeedBaseDataAsync();
        Guid serialId, templateId, defectId, jobId, reportId = Guid.NewGuid();
        await using (var db = CreateDbContext())
        {
            var store = DurableStore(db);
            serialId = store.RegisterSerial(new(1, "QA-C6-serial", "A-01", new DateOnly(2027, 1, 1), "QA-C6")).Id;
            store.TransitionSerial(serialId, new("AVAILABLE", "A-02", "QA-C6"));
            templateId = store.CreateChecklistTemplate(new("QA-C6-template", null, null, [new("QA-C6-check", true)], "QA-C6")).Id;
            defectId = store.CreateQcDefect(new(1, null, null, "DAMAGE", "LOW", "QA-C6-defect", "QA-C6")).Id;
            store.AddQcAttachment(defectId, new(Guid.NewGuid(), "QA-C6.txt", "text/plain", "/api/warehouse/QA-C6", DateTimeOffset.UtcNow, "QA-C6"));
            var queue = new DatabaseLabelPrintQueueStore(db);
            var job = queue.Enqueue("LOCATION", new Dictionary<string, string> { ["LocationCode"] = "QA-C6" });
            jobId = job.Id; job.Status = PrintQueueStatus.Failed; job.RetryCount = 10; queue.Save(job);
            new DatabaseAgnumReconciliationReportStore(db).Save(new(reportId, new DateOnly(2026, 11, 11), DateTimeOffset.UtcNow,
                [new("QA-C6", "RM-0001", "QA-C6", 3, 10, 30, 30, 0, 0)], new(0, 0, null, 0)));
        }
        await using var fresh = CreateDbContext();
        var stored = DurableStore(fresh);
        var serial = stored.SearchSerials("QA-C6", null, null).Single();
        serial.Id.Should().Be(serialId); serial.Status.Should().Be(SerialStatus.Available); serial.History.Should().HaveCount(2);
        var response = (Microsoft.AspNetCore.Mvc.OkObjectResult)new LKvitai.MES.Modules.Warehouse.Api.Controllers.SerialsController(stored).Search("QA-C6", null, null);
        var json = System.Text.Json.JsonSerializer.Serialize(response.Value);
        json.Should().Contain("\"Status\":\"Available\"").And.Contain("\"NewStatus\":\"Available\"");
        serial.Location.Should().Be("A-02"); serial.WarrantyExpiryDate.Should().Be(new DateOnly(2027, 1, 1));
        stored.GetChecklistTemplates().Single().Id.Should().Be(templateId);
        stored.GetQcDefects().Single().Attachments.Should().ContainSingle(); stored.GetQcDefectCount().Should().Be(1);
        var persisted = new DatabaseLabelPrintQueueStore(fresh).Get(jobId)!;
        persisted.Status.Should().Be(PrintQueueStatus.Failed); persisted.RetryCount.Should().Be(10);
        new DatabaseAgnumReconciliationReportStore(fresh).TryGet(reportId, out var report).Should().BeTrue();
        report!.Lines.Single().WarehouseValue.Should().Be(30);
        await using var query = _store!.QuerySession();
        (await CommittedEventData.ReadAsync<StockAdjustedEvent>(query, null, default)).Should().BeEmpty();
    }

    [SkippableFact]
    public async Task CrossDock_ValidatesRealLineAndOrder_PersistsLinkWithoutStockEvents()
    {
        DockerRequirement.EnsureEnabled(); await SeedBaseDataAsync(); await SeedAvailableStockAsync(5);
        await using var db = CreateDbContext();
        var sale = await Draft(db);
        (await Reservations(db).AllocateAsync(sale, () => sale.Allocate(sale.Id), default)).IsSuccess.Should().BeTrue();
        (await new ReleaseSalesOrderCommandHandler(db, new QuietBus()).Handle(new ReleaseSalesOrderCommand { SalesOrderId = sale.Id }, default)).IsSuccess.Should().BeTrue();
        var inbound = new InboundShipment { ReferenceNumber = "QA-C6-crossdock", Supplier = new Supplier { Code = "QA-C6", Name = "QA-C6" } };
        inbound.Lines.Add(new InboundShipmentLine { ItemId = 1, ExpectedQty = 2, BaseUoM = "PCS", UnitPrice = 10 });
        db.InboundShipments.Add(inbound); await db.SaveChangesAsync();
        var store = DurableStore(db);
        var missing = () => store.CreateCrossDock(new(int.MaxValue, sale.OutboundOrderId!.Value, 1, 1, "QA-C6"));
        missing.Should().Throw<WarehouseOperationValidationException>();
        var link = store.CreateCrossDock(new(inbound.Lines.Single().Id, sale.OutboundOrderId!.Value, 1, 1, "QA-C6"));
        store.UpdateCrossDockStatus(link.Id, "MATCHED");
        await using var fresh = CreateDbContext();
        var saved = DurableStore(fresh).GetCrossDocks().Single();
        saved.InboundShipmentLineId.Should().Be(inbound.Lines.Single().Id); saved.Status.Should().Be("MATCHED");
        await using var query = _store!.QuerySession();
        (await CommittedEventData.ReadAsync<StockMovedEvent>(query, null, default)).Should().BeEmpty();
    }
    [SkippableFact]
    public async Task CompletionMigration_IsReversible_WithoutChangingExistingStockTables()
    {
        DockerRequirement.EnsureEnabled(); await SeedBaseDataAsync();
        await using var db = CreateDbContext();
        var migration = new LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence.Migrations.PersistWarehouseOperations();
        var generator = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrationsSqlGenerator>(db);
        foreach (var operations in new[] { migration.DownOperations, migration.UpOperations, migration.DownOperations, migration.UpOperations })
            foreach (var command in generator.Generate(operations, db.Model))
                await db.Database.ExecuteSqlRawAsync(command.CommandText);
        (await db.Items.CountAsync()).Should().Be(1);
        (await db.Set<WarehouseRma>().CountAsync()).Should().Be(0);
        new DatabaseLabelPrintQueueStore(db).Enqueue("LOCATION", new Dictionary<string,string> { ["LocationCode"] = "QA-C6-migration" });
        await using var fresh = CreateDbContext();
        (await fresh.Set<LabelPrintJob>().CountAsync()).Should().Be(1);
    }

}
