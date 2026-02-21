using System.Collections.Concurrent;
using System.Text;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public enum LotTraceDirection
{
    Backward,
    Forward
}

public sealed record LotTraceNode(
    string NodeType,
    string NodeId,
    string NodeName,
    DateTimeOffset Timestamp,
    List<LotTraceNode> Children);

public sealed record LotTraceReport(
    Guid TraceId,
    string LotNumber,
    LotTraceDirection Direction,
    LotTraceNode Root,
    bool IsApproximate,
    DateTimeOffset GeneratedAt);

public sealed record LotTraceResult(
    bool Succeeded,
    LotTraceReport? Report,
    string? ErrorMessage,
    int StatusCode);

public interface ILotTraceStore
{
    void Save(LotTraceReport report);
    bool TryGet(Guid traceId, out LotTraceReport? report);
    bool TryGetAny(out LotTraceReport? report);
    int Count { get; }
}

public sealed class InMemoryLotTraceStore : ILotTraceStore
{
    private readonly ConcurrentDictionary<Guid, LotTraceReport> _reports = new();

    public void Save(LotTraceReport report)
    {
        _reports[report.TraceId] = report;
    }

    public bool TryGet(Guid traceId, out LotTraceReport? report)
    {
        if (_reports.TryGetValue(traceId, out var value))
        {
            report = value;
            return true;
        }

        report = null;
        return false;
    }

    public bool TryGetAny(out LotTraceReport? report)
    {
        if (_reports.IsEmpty)
        {
            report = null;
            return false;
        }

        report = _reports.Values.First();
        return true;
    }

    public int Count => _reports.Count;
}

public interface ILotTraceabilityService
{
    Task<LotTraceResult> BuildAsync(
        string lotNumber,
        LotTraceDirection direction,
        CancellationToken cancellationToken = default);

    string BuildCsv(LotTraceReport report);
}

public sealed class LotTraceabilityService : ILotTraceabilityService
{
    private readonly WarehouseDbContext _dbContext;

    public LotTraceabilityService(WarehouseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<LotTraceResult> BuildAsync(
        string lotNumber,
        LotTraceDirection direction,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(lotNumber))
        {
            return new LotTraceResult(false, null, "Lot number is required.", 400);
        }

        var normalizedLot = lotNumber.Trim();
        var normalizedLotLower = normalizedLot.ToLowerInvariant();

        var lot = await _dbContext.Lots
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.LotNumber.ToLower() == normalizedLotLower, cancellationToken);

        if (lot is null)
        {
            return new LotTraceResult(false, null, "Lot not found.", 404);
        }

        var item = await _dbContext.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == lot.ItemId, cancellationToken);

        if (item is null)
        {
            return new LotTraceResult(false, null, "Item for lot not found.", 404);
        }

        var rootTimestamp = lot.ProductionDate.HasValue
            ? new DateTimeOffset(lot.ProductionDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            : DateTimeOffset.UtcNow;

        var root = new LotTraceNode(
            "LOT",
            lot.LotNumber,
            $"{item.InternalSKU} - {item.Name}",
            rootTimestamp,
            []);

        var hasRelationships = false;

        if (direction == LotTraceDirection.Backward)
        {
            hasRelationships = await BuildBackwardAsync(root, lot, item.Id, cancellationToken);
        }
        else
        {
            hasRelationships = await BuildForwardAsync(root, lot, item.Id, cancellationToken);
        }

        var report = new LotTraceReport(
            Guid.NewGuid(),
            lot.LotNumber,
            direction,
            root,
            !hasRelationships,
            DateTimeOffset.UtcNow);

        return new LotTraceResult(true, report, null, 200);
    }

    public string BuildCsv(LotTraceReport report)
    {
        var rows = new List<LotTraceCsvRow>();
        Flatten(report.Root, parentNodeId: null, level: 0, rows);

        var sb = new StringBuilder();
        sb.AppendLine("Level,NodeType,NodeId,NodeName,Timestamp,ParentNodeId");

        foreach (var row in rows)
        {
            sb.Append(row.Level).Append(',');
            sb.Append(EscapeCsv(row.NodeType)).Append(',');
            sb.Append(EscapeCsv(row.NodeId)).Append(',');
            sb.Append(EscapeCsv(row.NodeName)).Append(',');
            sb.Append(EscapeCsv(row.Timestamp.ToString("O"))).Append(',');
            sb.Append(EscapeCsv(row.ParentNodeId));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private async Task<bool> BuildBackwardAsync(
        LotTraceNode root,
        Lot lot,
        int itemId,
        CancellationToken cancellationToken)
    {
        var inboundShipment = await _dbContext.InboundShipments
            .AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.Lines)
            .Where(x => x.Lines.Any(l => l.ItemId == itemId && l.ReceivedQty > 0m))
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (inboundShipment is null)
        {
            return false;
        }

        var shipmentTimestamp = inboundShipment.UpdatedAt ?? inboundShipment.CreatedAt;
        var shipmentNode = new LotTraceNode(
            "SHIPMENT",
            $"ISH-{inboundShipment.Id}",
            inboundShipment.ReferenceNumber,
            shipmentTimestamp,
            []);

        if (inboundShipment.Supplier is not null)
        {
            shipmentNode.Children.Add(new LotTraceNode(
                "SUPPLIER",
                inboundShipment.Supplier.Id.ToString(),
                inboundShipment.Supplier.Name,
                shipmentTimestamp,
                []));
        }

        root.Children.Add(shipmentNode);
        return true;
    }

    private async Task<bool> BuildForwardAsync(
        LotTraceNode root,
        Lot lot,
        int itemId,
        CancellationToken cancellationToken)
    {
        var salesOrders = await _dbContext.SalesOrders
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Lines)
            .Where(x => x.Lines.Any(l => l.ItemId == itemId && l.ShippedQty > 0m))
            .OrderBy(x => x.OrderDate)
            .ToListAsync(cancellationToken);

        if (salesOrders.Count == 0)
        {
            return false;
        }

        var salesOrderIds = salesOrders.Select(x => x.Id).ToList();
        var outboundOrders = await _dbContext.OutboundOrders
            .AsNoTracking()
            .Where(x => x.SalesOrderId.HasValue && salesOrderIds.Contains(x.SalesOrderId.Value))
            .ToListAsync(cancellationToken);

        foreach (var salesOrder in salesOrders)
        {
            var reservationId = salesOrder.ReservationId.HasValue && salesOrder.ReservationId.Value != Guid.Empty
                ? salesOrder.ReservationId.Value.ToString()
                : $"RES-{salesOrder.OrderNumber}";

            var reservationNode = new LotTraceNode(
                "RESERVATION",
                reservationId,
                $"Reservation for {salesOrder.OrderNumber}",
                salesOrder.CreatedAt,
                []);

            var outboundOrder = outboundOrders.FirstOrDefault(x => x.SalesOrderId == salesOrder.Id);
            if (outboundOrder is not null)
            {
                var outboundNode = new LotTraceNode(
                    "OUTBOUND_ORDER",
                    outboundOrder.OrderNumber,
                    outboundOrder.OrderNumber,
                    outboundOrder.OrderDate,
                    []);

                reservationNode.Children.Add(outboundNode);

                if (salesOrder.Customer is not null)
                {
                    outboundNode.Children.Add(new LotTraceNode(
                        "CUSTOMER",
                        salesOrder.Customer.CustomerCode,
                        salesOrder.Customer.Name,
                        salesOrder.ShippedAt ?? salesOrder.UpdatedAt ?? salesOrder.CreatedAt,
                        []));
                }
            }
            else if (salesOrder.Customer is not null)
            {
                reservationNode.Children.Add(new LotTraceNode(
                    "CUSTOMER",
                    salesOrder.Customer.CustomerCode,
                    salesOrder.Customer.Name,
                    salesOrder.ShippedAt ?? salesOrder.UpdatedAt ?? salesOrder.CreatedAt,
                    []));
            }

            root.Children.Add(reservationNode);
        }

        return true;
    }

    private static void Flatten(
        LotTraceNode node,
        string? parentNodeId,
        int level,
        ICollection<LotTraceCsvRow> rows)
    {
        rows.Add(new LotTraceCsvRow(level, node.NodeType, node.NodeId, node.NodeName, node.Timestamp, parentNodeId));

        foreach (var child in node.Children)
        {
            Flatten(child, node.NodeId, level + 1, rows);
        }
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private sealed record LotTraceCsvRow(
        int Level,
        string NodeType,
        string NodeId,
        string NodeName,
        DateTimeOffset Timestamp,
        string? ParentNodeId);
}
