using System.Text.Json;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Domain;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using IDocumentStore = Marten.IDocumentStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Reservation = LKvitai.MES.Modules.Warehouse.Domain.Aggregates.Reservation;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public sealed class WarehouseOperationValidationException(string message) : Exception(message);

public sealed partial class DatabaseWarehouseStore(WarehouseDbContext db, IDocumentStore events,
    IBalanceGuardLockFactory locks) : IAdvancedWarehouseStore
{
    private static void Require(bool valid, string message)
    {
        if (!valid) throw new WarehouseOperationValidationException(message);
    }

    public WaveRecord CreateWave(IReadOnlyCollection<Guid> orderIds, string? assignedOperator)
    {
        var ids = orderIds.Distinct().ToArray();
        Require(ids.Length > 0, "At least one released order is required.");
        var orders = db.OutboundOrders.Include(x => x.SalesOrder).Where(x => ids.Contains(x.Id)).ToList();
        Require(orders.Count == ids.Length && orders.All(x => x.SalesOrder?.Status == SalesOrderStatus.Picking &&
            x.Status == OutboundOrderStatus.Picking), "Every order must be released for picking.");
        Require(!db.Set<WarehouseWaveOrder>().Any(x => ids.Contains(x.OutboundOrderId)), "An order already belongs to a wave.");
        var taskOrders = ids.Select(x => x.ToString()).ToArray();
        Require(taskOrders.All(id => db.PickTasks.Any(x => x.OrderId == id)), "Every order must have real pick tasks.");
        var wave = new WarehouseWave { AssignedOperator = assignedOperator,
            AssignedAt = string.IsNullOrWhiteSpace(assignedOperator) ? null : DateTimeOffset.UtcNow,
            Status = string.IsNullOrWhiteSpace(assignedOperator) ? "Created" : "Assigned",
            Orders = ids.Select(id => new WarehouseWaveOrder { OutboundOrderId = id }).ToList() };
        db.Add(wave);
        db.SaveChanges();
        return MapWave(wave);
    }

    public IReadOnlyList<WaveRecord> GetWaves(string? status) => db.Set<WarehouseWave>().AsNoTracking().Include(x => x.Orders)
        .OrderByDescending(x => x.CreatedAt).ToList().Select(MapWave)
        .Where(x => string.IsNullOrWhiteSpace(status) || x.Status.ToString().Equals(status, StringComparison.OrdinalIgnoreCase)).ToList();

    public WaveRecord? GetWave(Guid id)
    {
        var wave = db.Set<WarehouseWave>().AsNoTracking().Include(x => x.Orders).SingleOrDefault(x => x.Id == id);
        return wave is null ? null : MapWave(wave);
    }

    public WaveRecord? AssignWave(Guid id, string assignedOperator)
    {
        Require(!string.IsNullOrWhiteSpace(assignedOperator), "An operator is required.");
        var wave = db.Set<WarehouseWave>().Include(x => x.Orders).SingleOrDefault(x => x.Id == id);
        if (wave is null) return null;
        Require(wave.Status is "Created" or "Assigned", "Only an unstarted wave can be assigned.");
        wave.AssignedOperator = assignedOperator; wave.AssignedAt = DateTimeOffset.UtcNow; wave.Status = "Assigned";
        var ids = wave.Orders.Select(x => x.OutboundOrderId.ToString()).ToArray();
        foreach (var task in db.PickTasks.Where(x => ids.Contains(x.OrderId) && x.Status == "Pending"))
            task.AssignedToUserId = assignedOperator;
        db.SaveChanges();
        return MapWave(wave);
    }

    public WaveRecord? StartWave(Guid id)
    {
        var wave = db.Set<WarehouseWave>().Include(x => x.Orders).SingleOrDefault(x => x.Id == id);
        if (wave is null) return null;
        Require(wave.Status is "Assigned" or "Picking", "Assign the wave before starting.");
        wave.Status = "Picking"; db.SaveChanges(); return MapWave(wave);
    }

    private WaveRecord MapWave(WarehouseWave wave)
    {
        var ids = wave.Orders.Select(x => x.OutboundOrderId).ToArray();
        var taskIds = ids.Select(x => x.ToString()).ToArray();
        var orders = db.OutboundOrders.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionary(x => x.Id);
        var tasks = db.PickTasks.AsNoTracking().Where(x => taskIds.Contains(x.OrderId)).ToList();
        var itemIds = tasks.Select(x => x.ItemId).Distinct().ToArray();
        var skus = db.Items.AsNoTracking().Where(x => itemIds.Contains(x.Id)).ToDictionary(x => x.Id, x => x.InternalSKU);
        var locations = db.Locations.AsNoTracking().ToDictionary(x => x.Id, x => x.Code);
        using var query = events.QuerySession();
        var reservedLocations = orders.Values.Where(x => x.SalesOrderId.HasValue).ToDictionary(x => x.Id,
            x => SalesReservationBalance.Load(query, x.SalesOrderId!.Value));
        var lines = tasks.Select(task => {
            var orderId = Guid.Parse(task.OrderId);
            var location = task.FromLocationId.HasValue ? locations.GetValueOrDefault(task.FromLocationId.Value, "–") :
                reservedLocations.TryGetValue(orderId, out var allocations) ? string.Join(", ", allocations
                    .Where(x => string.Equals(x.SKU, skus[task.ItemId], StringComparison.OrdinalIgnoreCase))
                    .Select(x => x.Location).Distinct().OrderBy(x => x, StringComparer.Ordinal)) : "–";
            return new WavePickLineRecord(task.ItemId, task.Qty, location, orderId, 0, task.TaskId, task.Status);
        }).OrderBy(x => x.Location, StringComparer.Ordinal).ThenBy(x => x.TaskId).ToList();
        var complete = tasks.Count > 0 && tasks.All(x => x.Status == "Completed");
        return new WaveRecord { Id = wave.Id, WaveNumber = $"WAVE-{wave.Number:0000}",
            Status = complete ? WaveStatus.Completed : Enum.Parse<WaveStatus>(wave.Status), CreatedAt = wave.CreatedAt,
            AssignedAt = wave.AssignedAt, AssignedOperator = wave.AssignedOperator,
            CompletedAt = complete ? tasks.Max(x => x.CompletedAt) : null, OrderIds = ids.ToList(),
            PickList = lines, CompletedLines = tasks.Count(x => x.Status == "Completed") };
    }

    public CrossDockRecord CreateCrossDock(CrossDockCreateRequest request)
    {
        Require(request.Qty > 0, "Quantity must be positive.");
        var line = db.InboundShipmentLines.AsNoTracking().SingleOrDefault(x => x.Id == request.InboundShipmentLineId);
        Require(line is not null && line.ItemId == request.ItemId, "Inbound line does not contain this item.");
        Require(db.OutboundOrders.Any(x => x.Id == request.OutboundOrderId && x.Lines.Any(l => l.ItemId == request.ItemId)),
            "Outbound order does not contain this item.");
        Require(request.Qty <= line!.ExpectedQty, "Quantity exceeds the inbound line.");
        Require(!db.Set<WarehouseCrossDockLink>().Any(x => x.InboundShipmentLineId == request.InboundShipmentLineId &&
            x.OutboundOrderId == request.OutboundOrderId), "This link already exists.");
        var row = new WarehouseCrossDockLink { InboundShipmentLineId = request.InboundShipmentLineId,
            OutboundOrderId = request.OutboundOrderId, ItemId = request.ItemId, Qty = request.Qty, CreatedBy = request.CreatedBy };
        db.Add(row); db.SaveChanges(); return MapCrossDock(row);
    }
    public IReadOnlyList<CrossDockRecord> GetCrossDocks() => db.Set<WarehouseCrossDockLink>().AsNoTracking()
        .OrderByDescending(x => x.CreatedAt).AsEnumerable().Select(MapCrossDock).ToList();
    public CrossDockRecord? UpdateCrossDockStatus(Guid id, string status)
    {
        var normalized = status.ToUpperInvariant();
        Require(normalized is "PENDING" or "MATCHED" or "COMPLETED", "Invalid cross-dock status.");
        var row = db.Set<WarehouseCrossDockLink>().Find(id);
        if (row is null) return null;
        row.Status = normalized; row.CompletedAt = normalized == "COMPLETED" ? DateTimeOffset.UtcNow : null;
        db.SaveChanges(); return MapCrossDock(row);
    }
    private static CrossDockRecord MapCrossDock(WarehouseCrossDockLink row) => new(row.Id, $"XD-{row.Number:0000}",
        row.InboundShipmentLineId, row.OutboundOrderId, row.ItemId, row.Qty, row.Status, row.CreatedAt, row.CompletedAt, row.CreatedBy);

    public QcChecklistTemplateRecord CreateChecklistTemplate(QcChecklistTemplateCreateRequest request)
    {
        Require(!string.IsNullOrWhiteSpace(request.Name) && request.Items.Count > 0 && request.Items.All(x => !string.IsNullOrWhiteSpace(x.Step)),
            "A name and checklist steps are required.");
        Require(!request.SupplierId.HasValue || db.Suppliers.Any(x => x.Id == request.SupplierId), "Supplier not found.");
        var row = new WarehouseQcTemplate { Name = request.Name.Trim(), CategoryCode = request.CategoryCode,
            SupplierId = request.SupplierId, CreatedBy = request.CreatedBy,
            ItemsJson = JsonSerializer.Serialize(request.Items.Select((x, i) => new QcChecklistItemRecord(Guid.NewGuid(), i + 1, x.Step, x.Required))) };
        db.Add(row); db.SaveChanges(); return MapTemplate(row);
    }
    public IReadOnlyList<QcChecklistTemplateRecord> GetChecklistTemplates() => db.Set<WarehouseQcTemplate>().AsNoTracking()
        .OrderBy(x => x.Name).AsEnumerable().Select(MapTemplate).ToList();
    private static QcChecklistTemplateRecord MapTemplate(WarehouseQcTemplate row) => new(row.Id, row.Name, row.CategoryCode,
        row.SupplierId, JsonSerializer.Deserialize<List<QcChecklistItemRecord>>(row.ItemsJson)!, row.CreatedAt, row.CreatedBy);

    public QcDefectRecord CreateQcDefect(QcDefectCreateRequest request)
    {
        Require(db.Items.Any(x => x.Id == request.ItemId), "Item not found.");
        Require(!request.SupplierId.HasValue || db.Suppliers.Any(x => x.Id == request.SupplierId), "Supplier not found.");
        Require(!string.IsNullOrWhiteSpace(request.DefectType) && !string.IsNullOrWhiteSpace(request.Severity), "Defect type and severity are required.");
        var row = new WarehouseQcDefect { ItemId = request.ItemId, LotNumber = request.LotNumber, SupplierId = request.SupplierId,
            DefectType = request.DefectType.ToUpperInvariant(), Severity = request.Severity.ToUpperInvariant(), Notes = request.Notes, CreatedBy = request.CreatedBy };
        db.Add(row); db.SaveChanges(); return MapDefect(row);
    }
    public IReadOnlyList<QcDefectRecord> GetQcDefects() => db.Set<WarehouseQcDefect>().AsNoTracking()
        .OrderByDescending(x => x.CreatedAt).AsEnumerable().Select(MapDefect).ToList();
    public QcDefectRecord? AddQcAttachment(Guid defectId, QcAttachmentRecord attachment)
    {
        return db.Database.CreateExecutionStrategy().Execute(() => {
            using var tx = db.Database.BeginTransaction();
            var row = db.Set<WarehouseQcDefect>().FromSqlInterpolated($"SELECT * FROM public.warehouse_qc_defects WHERE \"Id\"={defectId} FOR UPDATE").SingleOrDefault();
            if (row is null) return null;
            var attachments = JsonSerializer.Deserialize<List<QcAttachmentRecord>>(row.AttachmentsJson)!;
            attachments.Add(attachment); row.AttachmentsJson = JsonSerializer.Serialize(attachments);
            db.SaveChanges(); tx.Commit(); return MapDefect(row);
        });
    }
    private static QcDefectRecord MapDefect(WarehouseQcDefect row) => new(row.Id, row.ItemId, row.LotNumber, row.SupplierId,
        row.DefectType, row.Severity, row.Notes, row.CreatedAt, row.CreatedBy, JsonSerializer.Deserialize<List<QcAttachmentRecord>>(row.AttachmentsJson)!);
    public int GetQcDefectCount() => db.Set<WarehouseQcDefect>().Count();

    private static Guid SerialId(int id) => new(id, 0x4349, 0x4b41, 0x44, 0x41, 0x53, 0x45, 0x52, 0x49, 0x41, 0x4c);
    public SerialRecord RegisterSerial(SerialRegisterRequest request)
    {
        Require(db.Items.Any(x => x.Id == request.ItemId), "Item not found.");
        Require(!string.IsNullOrWhiteSpace(request.Value), "Serial value is required.");
        var value = request.Value.Trim();
        Require(!db.SerialNumbers.Any(x => x.Value.ToLower() == value.ToLower()), "Serial already exists.");
        var row = new SerialNumber { ItemId = request.ItemId, Value = value, Status = "Received" };
        db.Add(new SerialStatusHistory { SerialNumber = row, NewStatus = row.Status, Location = request.Location,
            WarrantyExpiryDate = request.WarrantyExpiryDate, UpdatedBy = request.UpdatedBy });
        db.SaveChanges(); return MapSerial(row);
    }
    public SerialRecord? TransitionSerial(Guid id, SerialTransitionRequest request)
    {
        Require(Enum.TryParse<SerialStatus>(request.Status, true, out var status) && Enum.IsDefined(status), "Invalid serial status.");
        var serialId = BitConverter.ToInt32(id.ToByteArray(), 0);
        if (SerialId(serialId) != id) return null;
        return db.Database.CreateExecutionStrategy().Execute(() => {
            using var tx = db.Database.BeginTransaction();
            var row = db.SerialNumbers.FromSqlInterpolated($"SELECT * FROM public.serial_numbers WHERE \"Id\"={serialId} FOR UPDATE").SingleOrDefault();
            if (row is null) return null;
            var latest = db.Set<SerialStatusHistory>().Where(x => x.SerialNumberId == row.Id).OrderByDescending(x => x.ChangedAt).FirstOrDefault();
            db.Add(new SerialStatusHistory { SerialNumberId = row.Id, PreviousStatus = row.Status, NewStatus = status.ToString(),
                Location = request.Location, WarrantyExpiryDate = latest?.WarrantyExpiryDate, UpdatedBy = request.UpdatedBy });
            row.Status = status.ToString(); db.SaveChanges(); tx.Commit(); return MapSerial(row);
        });
    }
    public IReadOnlyList<SerialRecord> SearchSerials(string? serial, int? itemId, string? status)
    {
        var query = db.SerialNumbers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(serial)) query = query.Where(x => x.Value.ToLower().Contains(serial.ToLower()));
        if (itemId.HasValue) query = query.Where(x => x.ItemId == itemId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status.ToLower() == status.ToLower());
        return query.OrderBy(x => x.Value).AsEnumerable().Select(MapSerial).ToList();
    }
    private SerialRecord MapSerial(SerialNumber row)
    {
        var history = db.Set<SerialStatusHistory>().AsNoTracking().Where(x => x.SerialNumberId == row.Id).OrderBy(x => x.ChangedAt).ToList();
        return new SerialRecord { Id = SerialId(row.Id), ItemId = row.ItemId, Value = row.Value,
            Status = Enum.Parse<SerialStatus>(row.Status, true), Location = history.LastOrDefault()?.Location,
            WarrantyExpiryDate = history.LastOrDefault()?.WarrantyExpiryDate,
            History = history.Select(x => new SerialHistoryRecord(x.ChangedAt, x.PreviousStatus is null ? null : Enum.Parse<SerialStatus>(x.PreviousStatus, true),
                Enum.Parse<SerialStatus>(x.NewStatus, true), x.Location, x.UpdatedBy)).ToList() };
    }
}
