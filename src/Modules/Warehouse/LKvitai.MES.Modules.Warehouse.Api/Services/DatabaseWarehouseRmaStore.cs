using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using IDocumentStore = Marten.IDocumentStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public sealed partial class DatabaseWarehouseStore
{
    public RmaRecord CreateRma(RmaCreateRequest request) => db.Database.CreateExecutionStrategy().Execute(() =>
    {
        Require(request.Lines.Count > 0 && request.Lines.All(x => x.Qty > 0), "Positive return quantities are required.");
        Require(!string.IsNullOrWhiteSpace(request.Reason), "Return reason is required.");
        using var tx = db.Database.BeginTransaction();
        var order = db.SalesOrders.FromSqlInterpolated($"SELECT * FROM public.sales_orders WHERE \"Id\"={request.SalesOrderId} FOR UPDATE").SingleOrDefault();
        Require(order is not null, "Sales order not found.");
        var orderLines = db.SalesOrderLines.Where(x => x.SalesOrderId == request.SalesOrderId).ToList();
        var lines = request.Lines.Select(line => {
            var candidates = orderLines.Where(x => x.ItemId == line.ItemId && (!line.SalesOrderLineId.HasValue || x.Id == line.SalesOrderLineId)).ToList();
            Require(candidates.Count == 1, "Select a specific line belonging to this order and item.");
            return new WarehouseRmaLine { SalesOrderLineId = candidates[0].Id, ItemId = line.ItemId, Qty = line.Qty, ReasonCode = line.ReasonCode };
        }).ToList();
        foreach (var group in lines.GroupBy(x => x.SalesOrderLineId))
        {
            var already = db.Set<WarehouseRmaLine>().Where(x => x.SalesOrderLineId == group.Key).Sum(x => (decimal?)x.Qty) ?? 0m;
            Require(already + group.Sum(x => x.Qty) <= orderLines.Single(x => x.Id == group.Key).OrderedQty,
                "Return quantity exceeds the order line, including previous RMAs.");
        }
        var row = new WarehouseRma { SalesOrderId = request.SalesOrderId, Reason = request.Reason, CreatedBy = request.CreatedBy, Lines = lines };
        db.Add(row); db.SaveChanges(); tx.Commit(); return MapRma(row);
    });

    public IReadOnlyList<RmaRecord> GetRmas() => db.Set<WarehouseRma>().AsNoTracking().Include(x => x.Lines)
        .OrderByDescending(x => x.CreatedAt).AsEnumerable().Select(MapRma).ToList();

    public Task<RmaRecord?> ReceiveRmaAsync(Guid id, string user, CancellationToken ct = default) => ChangeRmaStockAsync(id, user, null, null, ct);
    public Task<RmaRecord?> InspectRmaAsync(Guid id, string disposition, decimal? creditAmount, string user, CancellationToken ct = default)
    {
        var normalized = disposition.ToUpperInvariant();
        Require(normalized is "RESTOCK" or "SCRAP", "Disposition must be RESTOCK or SCRAP.");
        Require(!creditAmount.HasValue || creditAmount >= 0, "Credit amount cannot be negative.");
        return ChangeRmaStockAsync(id, user, normalized, creditAmount, ct);
    }

    private async Task<RmaRecord?> ChangeRmaStockAsync(Guid id, string user, string? disposition, decimal? credit, CancellationToken ct)
    {
        var itemIds = await db.Set<WarehouseRmaLine>().Where(x => x.RmaId == id).Select(x => x.ItemId).Distinct().ToArrayAsync(ct);
        var items = await db.Items.Where(x => itemIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        await using var guard = await locks.CreateAsync(ct);
        await guard.AcquireAsync(items.Values.Select(x => StockLockKey.ForLocation("WH1", "QC_HOLD", x.InternalSKU)).Distinct().Order().ToArray(), ct);
        var result = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var row = await db.Set<WarehouseRma>().FromSqlInterpolated($"SELECT * FROM public.warehouse_rmas WHERE \"Id\"={id} FOR UPDATE").SingleOrDefaultAsync(ct);
            if (row is null) return null;
            await db.Entry(row).Collection(x => x.Lines).LoadAsync(ct);
            var isReceipt = disposition is null;
            if (isReceipt && row.ReceivedAt.HasValue) return MapRma(row);
            if (!isReceipt && row.InspectedAt.HasValue)
            {
                Require(row.Disposition == disposition && row.CreditAmount == credit, "RMA was already inspected with a different result.");
                return MapRma(row);
            }
            Require(isReceipt ? row.Status == "PendingReceipt" : row.Status == "Received", "Invalid RMA transition.");
            var hold = await db.Locations.SingleAsync(x => x.Code == "QC_HOLD", ct);
            var destination = disposition == "SCRAP" ? "SCRAP" : "RECEIVING";
            await using var session = events.LightweightSession(Marten.Services.SessionOptions.ForTransaction((NpgsqlTransaction)tx.GetDbTransaction(), false));
            foreach (var line in row.Lines.GroupBy(x => x.ItemId))
            {
                var qty = line.Sum(x => x.Qty);
                var item = items[line.Key];
                if (isReceipt)
                {
                    var adjustmentId = Guid.NewGuid();
                    session.Events.StartStream($"stock-adjustment:{adjustmentId:N}", new StockAdjustedEvent {
                        AggregateId = adjustmentId, AdjustmentId = adjustmentId, UserId = user, WarehouseId = "WH1",
                        ItemId = item.Id, SKU = item.InternalSKU, LocationId = hold.Id, Location = "QC_HOLD", QtyDelta = qty,
                        ReasonCode = "RMA", Notes = $"{row.Reason}; RMA-{row.Number:0000}", Timestamp = DateTime.UtcNow });
                }
                else
                {
                    Require(await CommittedLocationStock.ReadAsync(events, item.InternalSKU, "QC_HOLD", ct) >= qty,
                        "Insufficient committed stock in QC_HOLD.");
                    session.Events.Append(StockLedgerStreamId.For("WH1", "QC_HOLD", item.InternalSKU), new StockMovedEvent {
                        MovementId = Guid.NewGuid(), SKU = item.InternalSKU, Quantity = qty, FromLocation = "QC_HOLD",
                        ToLocation = destination, OperatorId = Guid.TryParse(user, out var operatorId) ? operatorId : Guid.Empty, MovementType = "RMA", Timestamp = DateTime.UtcNow, Reason = $"{row.Reason}; RMA-{row.Number:0000}" });
                }
            }
            row.UpdatedBy = user;
            if (isReceipt) { row.Status = "Received"; row.ReceivedAt = DateTimeOffset.UtcNow; }
            else { row.Status = disposition == "SCRAP" ? "Scrapped" : "Restocked"; row.Disposition = disposition;
                row.CreditAmount = credit; row.InspectedAt = DateTimeOffset.UtcNow; }
            await db.SaveChangesAsync(false, ct);
            await session.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            db.ChangeTracker.AcceptAllChanges();
            return MapRma(row);
        });
        await guard.CommitAsync(ct);
        return result;
    }
    private static RmaRecord MapRma(WarehouseRma row) => new() { Id = row.Id, RmaNumber = $"RMA-{row.Number:0000}",
        SalesOrderId = row.SalesOrderId, Reason = row.Reason, Status = Enum.Parse<RmaStatus>(row.Status), CreatedAt = row.CreatedAt,
        ReceivedAt = row.ReceivedAt, InspectedAt = row.InspectedAt, Disposition = row.Disposition, CreditAmount = row.CreditAmount,
        CreatedBy = row.CreatedBy, UpdatedBy = row.UpdatedBy,
        Lines = row.Lines.Select(x => new RmaLineRecord(x.Id, x.ItemId, x.Qty, x.ReasonCode, x.SalesOrderLineId)).ToList() };
}
