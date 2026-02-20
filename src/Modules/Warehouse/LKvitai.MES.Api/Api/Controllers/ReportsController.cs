using System.Globalization;
using System.Text;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using IDocumentStore = Marten.IDocumentStore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly WarehouseDbContext _dbContext;
    private readonly IDocumentStore _documentStore;

    public ReportsController(WarehouseDbContext dbContext, IDocumentStore documentStore)
    {
        _dbContext = dbContext;
        _documentStore = documentStore;
    }

    [HttpGet("dispatch-history")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetDispatchHistoryAsync(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? carrier,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool exportCsv = false,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var fromUtc = from?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toExclusiveUtc = to?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var query = _dbContext.DispatchHistories
            .AsNoTracking()
            .AsQueryable();

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.DispatchedAt >= fromUtc.Value);
        }

        if (toExclusiveUtc.HasValue)
        {
            query = query.Where(x => x.DispatchedAt < toExclusiveUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(carrier))
        {
            var carrierFilter = carrier.Trim();
            query = query.Where(x => x.Carrier == carrierFilter);
        }

        var baseRows = await query
            .OrderByDescending(x => x.DispatchedAt)
            .ToListAsync(cancellationToken);

        var shipmentIds = baseRows.Select(x => x.ShipmentId).Distinct().ToList();
        var shipments = await _dbContext.Shipments
            .AsNoTracking()
            .Where(x => shipmentIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var outboundOrderIds = shipments.Values.Select(x => x.OutboundOrderId).Distinct().ToList();
        var outboundOrders = await _dbContext.OutboundOrders
            .AsNoTracking()
            .Include(x => x.SalesOrder)
                .ThenInclude(x => x!.Customer)
            .Where(x => outboundOrderIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var mapped = baseRows.Select(row =>
        {
            shipments.TryGetValue(row.ShipmentId, out var shipment);
            OutboundOrder? outboundOrder = null;
            if (shipment is not null)
            {
                outboundOrders.TryGetValue(shipment.OutboundOrderId, out outboundOrder);
            }

            var orderNumber = outboundOrder?.SalesOrder?.OrderNumber ?? row.OutboundOrderNumber;
            var customerName = outboundOrder?.SalesOrder?.Customer?.Name;
            var requestedDelivery = outboundOrder?.RequestedShipDate?.UtcDateTime;
            var deliveredAt = shipment?.DeliveredAt?.UtcDateTime;
            var status = deliveredAt.HasValue ? "DELIVERED" : "DISPATCHED";

            return new DispatchHistoryReportRow(
                row.ShipmentId,
                row.ShipmentNumber,
                orderNumber,
                customerName,
                row.Carrier,
                row.TrackingNumber,
                row.DispatchedAt.UtcDateTime,
                deliveredAt,
                requestedDelivery,
                status);
        }).ToList();

        var onTimeDelivered = mapped
            .Where(x => x.DeliveryDate.HasValue && x.RequestedDeliveryDate.HasValue && x.DeliveryDate.Value <= x.RequestedDeliveryDate.Value)
            .Count();
        var deliveredCount = mapped.Count(x => x.DeliveryDate.HasValue && x.RequestedDeliveryDate.HasValue);
        var onTimePercent = deliveredCount == 0
            ? 0m
            : Math.Round(onTimeDelivered * 100m / deliveredCount, 1, MidpointRounding.AwayFromZero);

        if (exportCsv)
        {
            return File(
                Encoding.UTF8.GetBytes(BuildDispatchCsv(mapped)),
                "text/csv",
                $"dispatch-history-{DateTime.UtcNow:yyyy-MM-dd}.csv");
        }

        var paged = mapped
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var response = new DispatchHistoryReportResponse(
            new DispatchHistorySummary(
                mapped.Count(),
                mapped.Select(x => x.OrderNumber).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                onTimePercent),
            paged,
            mapped.Count(),
            page,
            pageSize);

        return Ok(response);
    }

    [HttpGet("stock-movements")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetStockMovementsAsync(
        [FromQuery] DateTimeOffset? startDate,
        [FromQuery] DateTimeOffset? endDate,
        [FromQuery] int? itemId,
        [FromQuery] int? locationId,
        [FromQuery] string? operatorId,
        [FromQuery] string? movementType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool exportCsv = false,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var from = startDate ?? DateTimeOffset.UtcNow.AddDays(-7);
        var to = endDate ?? DateTimeOffset.UtcNow;

        var rows = await BuildStockMovementsAsync(from, to, cancellationToken);

        if (itemId.HasValue)
        {
            rows = rows.Where(x => x.ItemId == itemId.Value).ToList();
        }

        if (locationId.HasValue)
        {
            rows = rows
                .Where(x => x.FromLocationId == locationId.Value || x.ToLocationId == locationId.Value)
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(operatorId))
        {
            var op = operatorId.Trim();
            rows = rows.Where(x => string.Equals(x.Operator, op, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(movementType))
        {
            var type = movementType.Trim();
            rows = rows.Where(x => string.Equals(x.MovementType, type, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        rows = rows
            .OrderByDescending(x => x.Timestamp)
            .ToList();

        if (exportCsv)
        {
            return File(
                Encoding.UTF8.GetBytes(BuildStockMovementsCsv(rows)),
                "text/csv",
                $"stock-movements-{DateTime.UtcNow:yyyy-MM-dd}.csv");
        }

        var paged = rows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new StockMovementsResponse(paged, rows.Count, page, pageSize));
    }

    [HttpGet("traceability")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetTraceabilityAsync(
        [FromQuery] string? lotNumber,
        [FromQuery] string? itemSku,
        [FromQuery] string? salesOrder,
        [FromQuery] string? supplier,
        CancellationToken cancellationToken = default)
    {
        var lotsQuery = _dbContext.Lots
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(lotNumber))
        {
            var lotFilter = lotNumber.Trim();
            lotsQuery = lotsQuery.Where(x => x.LotNumber == lotFilter);
        }

        if (!string.IsNullOrWhiteSpace(itemSku))
        {
            var skuFilter = itemSku.Trim();
            var itemIds = await _dbContext.Items
                .AsNoTracking()
                .Where(x => x.InternalSKU.Contains(skuFilter))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            lotsQuery = lotsQuery.Where(x => itemIds.Contains(x.ItemId));
        }

        if (!string.IsNullOrWhiteSpace(salesOrder))
        {
            var orderFilter = salesOrder.Trim();
            var salesItemIds = await _dbContext.SalesOrders
                .AsNoTracking()
                .Include(x => x.Lines)
                .Where(x => x.OrderNumber.Contains(orderFilter))
                .SelectMany(x => x.Lines.Select(l => l.ItemId))
                .Distinct()
                .ToListAsync(cancellationToken);

            lotsQuery = lotsQuery.Where(x => salesItemIds.Contains(x.ItemId));
        }

        if (!string.IsNullOrWhiteSpace(supplier))
        {
            var supplierFilter = supplier.Trim();
            var inboundItemIds = await _dbContext.InboundShipments
                .AsNoTracking()
                .Include(x => x.Supplier)
                .Include(x => x.Lines)
                .Where(x => x.Supplier != null && x.Supplier.Name.Contains(supplierFilter))
                .SelectMany(x => x.Lines.Select(l => l.ItemId))
                .Distinct()
                .ToListAsync(cancellationToken);

            lotsQuery = lotsQuery.Where(x => inboundItemIds.Contains(x.ItemId));
        }

        var lots = await lotsQuery
            .OrderBy(x => x.LotNumber)
            .Take(100)
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(lotNumber) && lots.Count == 0)
        {
            return NotFound(new { message = "Lot not found." });
        }

        var entries = new List<TraceabilityEntryResponse>();
        foreach (var lot in lots)
        {
            var entry = await BuildTraceabilityEntryAsync(lot, cancellationToken);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        return Ok(new TraceabilityResponse(entries));
    }

    [HttpGet("compliance-audit")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> GetComplianceAuditAsync(
        [FromQuery] DateTimeOffset? startDate,
        [FromQuery] DateTimeOffset? endDate,
        [FromQuery] string? reportType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool exportCsv = false,
        [FromQuery] bool exportPdf = false,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var from = startDate ?? DateTimeOffset.UtcNow.AddDays(-30);
        var to = endDate ?? DateTimeOffset.UtcNow;
        var type = string.IsNullOrWhiteSpace(reportType) ? "STOCK_MOVEMENTS" : reportType.Trim().ToUpperInvariant();

        var entries = type switch
        {
            "STOCK_MOVEMENTS" => (await BuildStockMovementsAsync(from, to, cancellationToken))
                .Select(x => new ComplianceAuditRow(
                    x.Timestamp,
                    "StockMovements",
                    x.Operator ?? "system",
                    x.Reference,
                    x.MovementType,
                    $"{x.ItemSku} {x.FromLocationCode} -> {x.ToLocationCode} Qty {x.Qty} {x.BaseUoM}",
                    x.Reason))
                .ToList(),
            "ADJUSTMENTS" => await BuildAdjustmentAuditAsync(from, to, cancellationToken),
            "VALUATIONS" => await BuildValuationAuditAsync(from, to, cancellationToken),
            "USER_ACTIONS" => await BuildUserActionsAuditAsync(from, to, cancellationToken),
            _ => []
        };

        entries = entries
            .OrderByDescending(x => x.Timestamp)
            .ToList();

        if (exportCsv)
        {
            return File(
                Encoding.UTF8.GetBytes(BuildComplianceCsv(entries)),
                "text/csv",
                $"compliance-audit-{DateTime.UtcNow:yyyy-MM-dd}.csv");
        }

        if (exportPdf)
        {
            var lines = entries
                .Take(120)
                .Select(x => $"{x.Timestamp:yyyy-MM-dd HH:mm} | {x.ReportType} | {x.Actor} | {x.Reference} | {x.Category}")
                .ToList();
            var pdf = SimplePdfBuilder.BuildSinglePage("Compliance Audit Report", lines);
            return File(pdf, "application/pdf", $"compliance-audit-{DateTime.UtcNow:yyyy-MM-dd}.pdf");
        }

        var paged = entries
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new ComplianceAuditResponse(paged, entries.Count, page, pageSize));
    }

    private async Task<List<StockMovementRow>> BuildStockMovementsAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var locationById = await _dbContext.Locations
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var locationByCode = locationById.Values
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var itemById = await _dbContext.Items
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var itemBySku = itemById.Values
            .GroupBy(x => x.InternalSKU, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var rows = new List<StockMovementRow>();

        var inboundShipments = await _dbContext.InboundShipments
            .AsNoTracking()
            .Include(x => x.Lines)
            .Where(x => (x.UpdatedAt ?? x.CreatedAt) >= from && (x.UpdatedAt ?? x.CreatedAt) <= to)
            .ToListAsync(cancellationToken);

        foreach (var shipment in inboundShipments)
        {
            var timestamp = shipment.UpdatedAt ?? shipment.CreatedAt;
            foreach (var line in shipment.Lines.Where(x => x.ReceivedQty > 0))
            {
                if (!itemById.TryGetValue(line.ItemId, out var item))
                {
                    continue;
                }

                var destinationCode = item.RequiresQC ? "QC_HOLD" : "RECEIVING";
                locationByCode.TryGetValue(destinationCode, out var destination);

                rows.Add(new StockMovementRow(
                    timestamp,
                    "Receive",
                    item.Id,
                    item.InternalSKU,
                    item.Name,
                    null,
                    "SUPPLIER",
                    destination?.Id,
                    destinationCode,
                    line.ReceivedQty,
                    line.BaseUoM,
                    shipment.UpdatedBy ?? shipment.CreatedBy ?? "system",
                    "Goods receipt",
                    $"ISH-{shipment.Id}"));
            }
        }

        var transfers = await _dbContext.Transfers
            .AsNoTracking()
            .Include(x => x.Lines)
            .Where(x => x.ExecutedAt.HasValue && x.ExecutedAt >= from && x.ExecutedAt <= to)
            .ToListAsync(cancellationToken);

        foreach (var transfer in transfers)
        {
            var timestamp = transfer.ExecutedAt ?? transfer.CompletedAt ?? transfer.UpdatedAt ?? transfer.CreatedAt;
            foreach (var line in transfer.Lines)
            {
                itemById.TryGetValue(line.ItemId, out var item);
                locationById.TryGetValue(line.FromLocationId, out var fromLocation);
                locationById.TryGetValue(line.ToLocationId, out var toLocation);

                rows.Add(new StockMovementRow(
                    timestamp,
                    "Transfer",
                    line.ItemId,
                    item?.InternalSKU ?? line.ItemId.ToString(CultureInfo.InvariantCulture),
                    item?.Name ?? string.Empty,
                    fromLocation?.Id,
                    fromLocation?.Code ?? line.FromLocationId.ToString(CultureInfo.InvariantCulture),
                    toLocation?.Id,
                    toLocation?.Code ?? line.ToLocationId.ToString(CultureInfo.InvariantCulture),
                    line.Qty,
                    item?.BaseUoM ?? string.Empty,
                    transfer.UpdatedBy ?? transfer.RequestedBy,
                    $"Transfer {transfer.TransferNumber}",
                    transfer.TransferNumber));
            }
        }

        var pickTasks = await _dbContext.PickTasks
            .AsNoTracking()
            .Where(x => x.CompletedAt.HasValue && x.CompletedAt >= from && x.CompletedAt <= to && x.Status == "Completed")
            .ToListAsync(cancellationToken);

        foreach (var task in pickTasks)
        {
            itemById.TryGetValue(task.ItemId, out var item);
            Location? fromLocation = null;
            if (task.FromLocationId.HasValue)
            {
                locationById.TryGetValue(task.FromLocationId.Value, out fromLocation);
            }

            Location? toLocation = null;
            if (task.ToLocationId.HasValue)
            {
                locationById.TryGetValue(task.ToLocationId.Value, out toLocation);
            }

            rows.Add(new StockMovementRow(
                task.CompletedAt ?? task.UpdatedAt ?? task.CreatedAt,
                "Pick",
                task.ItemId,
                item?.InternalSKU ?? task.ItemId.ToString(CultureInfo.InvariantCulture),
                item?.Name ?? string.Empty,
                fromLocation?.Id,
                fromLocation?.Code ?? string.Empty,
                toLocation?.Id,
                toLocation?.Code ?? string.Empty,
                task.PickedQty ?? task.Qty,
                item?.BaseUoM ?? string.Empty,
                task.UpdatedBy ?? task.AssignedToUserId ?? "system",
                $"Pick task {task.TaskId}",
                task.OrderId));
        }

        await using (var querySession = _documentStore.QuerySession())
        {
            var adjustments = await Marten.QueryableExtensions.ToListAsync(
                querySession.Query<AdjustmentHistoryView>()
                    .Where(x => x.Timestamp >= from && x.Timestamp <= to),
                cancellationToken);

            foreach (var adjustment in adjustments)
            {
                if (!itemBySku.TryGetValue(adjustment.SKU, out var item))
                {
                    continue;
                }

                locationByCode.TryGetValue(adjustment.LocationCode ?? adjustment.Location, out var location);

                var fromLocationCode = adjustment.QtyDelta < 0 ? adjustment.LocationCode ?? adjustment.Location : "ADJUSTMENT";
                var toLocationCode = adjustment.QtyDelta >= 0 ? adjustment.LocationCode ?? adjustment.Location : "ADJUSTMENT";

                rows.Add(new StockMovementRow(
                    adjustment.Timestamp,
                    "Adjust",
                    item.Id,
                    item.InternalSKU,
                    item.Name,
                    adjustment.QtyDelta < 0 ? location?.Id : null,
                    fromLocationCode,
                    adjustment.QtyDelta >= 0 ? location?.Id : null,
                    toLocationCode,
                    Math.Abs(adjustment.QtyDelta),
                    item.BaseUoM,
                    adjustment.UserId,
                    adjustment.ReasonCode,
                    adjustment.AdjustmentId.ToString()));
            }
        }

        var shipments = await _dbContext.Shipments
            .AsNoTracking()
            .Include(x => x.Lines)
            .Where(x => x.DispatchedAt.HasValue && x.DispatchedAt >= from && x.DispatchedAt <= to)
            .ToListAsync(cancellationToken);

        foreach (var shipment in shipments)
        {
            var timestamp = shipment.DispatchedAt ?? shipment.UpdatedAt ?? shipment.CreatedAt;
            foreach (var line in shipment.Lines)
            {
                itemById.TryGetValue(line.ItemId, out var item);
                rows.Add(new StockMovementRow(
                    timestamp,
                    "Dispatch",
                    line.ItemId,
                    item?.InternalSKU ?? line.ItemId.ToString(CultureInfo.InvariantCulture),
                    item?.Name ?? string.Empty,
                    null,
                    "SHIPPING",
                    null,
                    "CUSTOMER",
                    line.Qty,
                    item?.BaseUoM ?? string.Empty,
                    shipment.UpdatedBy ?? "system",
                    $"Shipment {shipment.ShipmentNumber}",
                    shipment.ShipmentNumber));
            }
        }

        return rows;
    }

    private async Task<TraceabilityEntryResponse?> BuildTraceabilityEntryAsync(
        LKvitai.MES.Domain.Entities.Lot lot,
        CancellationToken cancellationToken)
    {
        var item = await _dbContext.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == lot.ItemId, cancellationToken);
        if (item is null)
        {
            return null;
        }

        var inboundShipment = await _dbContext.InboundShipments
            .AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.Lines)
            .Where(x => x.Lines.Any(l => l.ItemId == item.Id && l.ReceivedQty > 0m))
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var upstream = inboundShipment is null
            ? null
            : new TraceabilityUpstream(
                inboundShipment.Supplier?.Name ?? string.Empty,
                (inboundShipment.UpdatedAt ?? inboundShipment.CreatedAt).UtcDateTime,
                inboundShipment.ReferenceNumber,
                inboundShipment.Lines.Where(x => x.ItemId == item.Id).Sum(x => x.ReceivedQty));

        decimal currentQty;
        string? currentLocation;
        await using (var querySession = _documentStore.QuerySession())
        {
            var stockRows = await Marten.QueryableExtensions.ToListAsync(
                querySession.Query<AvailableStockView>()
                    .Where(x => x.SKU == item.InternalSKU &&
                                x.LotNumber == lot.LotNumber),
                cancellationToken);

            currentQty = stockRows.Sum(x => x.AvailableQty);
            currentLocation = stockRows
                .OrderByDescending(x => x.AvailableQty)
                .Select(x => x.LocationCode ?? x.Location)
                .FirstOrDefault();
        }

        var current = new TraceabilityCurrent(currentLocation, currentQty);

        var salesOrders = await _dbContext.SalesOrders
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Lines)
            .Where(x => x.Lines.Any(l => l.ItemId == item.Id && l.ShippedQty > 0m))
            .ToListAsync(cancellationToken);

        var salesOrderIds = salesOrders.Select(x => x.Id).ToList();
        var outboundOrders = await _dbContext.OutboundOrders
            .AsNoTracking()
            .Include(x => x.Shipment)
            .Where(x => x.SalesOrderId.HasValue && salesOrderIds.Contains(x.SalesOrderId.Value))
            .ToListAsync(cancellationToken);

        var downstreamSales = salesOrders
            .Select(order =>
            {
                var lineQty = order.Lines.Where(x => x.ItemId == item.Id).Sum(x => x.ShippedQty);
                var outbound = outboundOrders.FirstOrDefault(x => x.SalesOrderId == order.Id);
                return new TraceabilitySalesOrder(
                    order.OrderNumber,
                    order.Customer?.Name ?? string.Empty,
                    lineQty,
                    order.ShippedAt?.UtcDateTime,
                    outbound?.Shipment?.TrackingNumber);
            })
            .Where(x => x.QtyShipped > 0m)
            .ToList();

        var downstream = new TraceabilityDownstream(
            [],
            downstreamSales);

        return new TraceabilityEntryResponse(
            new TraceabilityLot(lot.LotNumber, item.InternalSKU, item.Name),
            upstream,
            current,
            downstream,
            true);
    }

    private async Task<List<ComplianceAuditRow>> BuildAdjustmentAuditAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        await using var querySession = _documentStore.QuerySession();
        var rows = await Marten.QueryableExtensions.ToListAsync(
            querySession.Query<AdjustmentHistoryView>()
                .Where(x => x.Timestamp >= from && x.Timestamp <= to),
            cancellationToken);

        return rows.Select(x => new ComplianceAuditRow(
            x.Timestamp,
            "Adjustments",
            x.UserId,
            x.AdjustmentId.ToString(),
            x.ReasonCode,
            $"{x.SKU} {x.QtyDelta}",
            x.Notes))
            .ToList();
    }

    private async Task<List<ComplianceAuditRow>> BuildValuationAuditAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.OnHandValues
            .AsNoTracking()
            .Where(x => x.LastUpdated >= from && x.LastUpdated <= to)
            .OrderByDescending(x => x.LastUpdated)
            .ToListAsync(cancellationToken);

        return rows.Select(x => new ComplianceAuditRow(
            x.LastUpdated,
            "Valuations",
            "system",
            x.ItemSku,
            "OnHandValue",
            $"{x.ItemSku} Qty {x.Qty} UnitCost {x.UnitCost}",
            $"TotalValue={x.TotalValue.ToString(CultureInfo.InvariantCulture)}"))
            .ToList();
    }

    private async Task<List<ComplianceAuditRow>> BuildUserActionsAuditAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var result = new List<ComplianceAuditRow>();

        var salesOrders = await _dbContext.SalesOrders
            .AsNoTracking()
            .Where(x => x.CreatedAt >= from && x.CreatedAt <= to)
            .ToListAsync(cancellationToken);
        result.AddRange(salesOrders.Select(x => new ComplianceAuditRow(
            x.CreatedAt,
            "UserActions",
            x.CreatedBy ?? "system",
            x.OrderNumber,
            "SalesOrderCreated",
            $"Customer={x.CustomerId}",
            null)));

        var transfers = await _dbContext.Transfers
            .AsNoTracking()
            .Where(x => x.RequestedAt >= from && x.RequestedAt <= to)
            .ToListAsync(cancellationToken);
        result.AddRange(transfers.Select(x => new ComplianceAuditRow(
            x.RequestedAt,
            "UserActions",
            x.RequestedBy,
            x.TransferNumber,
            "TransferRequested",
            $"{x.FromWarehouse}->{x.ToWarehouse}",
            null)));

        var inbound = await _dbContext.InboundShipments
            .AsNoTracking()
            .Where(x => x.CreatedAt >= from && x.CreatedAt <= to)
            .ToListAsync(cancellationToken);
        result.AddRange(inbound.Select(x => new ComplianceAuditRow(
            x.CreatedAt,
            "UserActions",
            x.CreatedBy ?? "system",
            $"ISH-{x.Id}",
            "InboundShipmentCreated",
            x.ReferenceNumber,
            null)));

        var dispatch = await _dbContext.DispatchHistories
            .AsNoTracking()
            .Where(x => x.DispatchedAt >= from && x.DispatchedAt <= to)
            .ToListAsync(cancellationToken);
        result.AddRange(dispatch.Select(x => new ComplianceAuditRow(
            x.DispatchedAt,
            "UserActions",
            x.DispatchedBy,
            x.ShipmentNumber,
            "ShipmentDispatched",
            x.OutboundOrderNumber,
            x.TrackingNumber)));

        return result;
    }

    private static string BuildDispatchCsv(IReadOnlyCollection<DispatchHistoryReportRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ShipmentNumber,OrderNumber,Customer,Carrier,TrackingNumber,DispatchDate,DeliveryDate,RequestedDeliveryDate,Status");
        foreach (var row in rows)
        {
            sb.Append(EscapeCsv(row.ShipmentNumber)).Append(',');
            sb.Append(EscapeCsv(row.OrderNumber)).Append(',');
            sb.Append(EscapeCsv(row.CustomerName)).Append(',');
            sb.Append(EscapeCsv(row.Carrier)).Append(',');
            sb.Append(EscapeCsv(row.TrackingNumber)).Append(',');
            sb.Append(row.DispatchDate.ToString("O", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(row.DeliveryDate?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty).Append(',');
            sb.Append(row.RequestedDeliveryDate?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty).Append(',');
            sb.Append(EscapeCsv(row.Status));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string BuildStockMovementsCsv(IReadOnlyCollection<StockMovementRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,MovementType,ItemSku,ItemName,FromLocation,ToLocation,Qty,UoM,Operator,Reason,Reference");
        foreach (var row in rows)
        {
            sb.Append(row.Timestamp.ToString("O", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv(row.MovementType)).Append(',');
            sb.Append(EscapeCsv(row.ItemSku)).Append(',');
            sb.Append(EscapeCsv(row.ItemName)).Append(',');
            sb.Append(EscapeCsv(row.FromLocationCode)).Append(',');
            sb.Append(EscapeCsv(row.ToLocationCode)).Append(',');
            sb.Append(row.Qty.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv(row.BaseUoM)).Append(',');
            sb.Append(EscapeCsv(row.Operator)).Append(',');
            sb.Append(EscapeCsv(row.Reason)).Append(',');
            sb.Append(EscapeCsv(row.Reference));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string BuildComplianceCsv(IReadOnlyCollection<ComplianceAuditRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Timestamp,ReportType,Actor,Reference,Category,Details,Notes");
        foreach (var row in rows)
        {
            sb.Append(row.Timestamp.ToString("O", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv(row.ReportType)).Append(',');
            sb.Append(EscapeCsv(row.Actor)).Append(',');
            sb.Append(EscapeCsv(row.Reference)).Append(',');
            sb.Append(EscapeCsv(row.Category)).Append(',');
            sb.Append(EscapeCsv(row.Details)).Append(',');
            sb.Append(EscapeCsv(row.Notes));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return '"' + value.Replace("\"", "\"\"") + '"';
    }

    public sealed record DispatchHistorySummary(int TotalShipments, int TotalOrders, decimal OnTimeDeliveryPercent);

    public sealed record DispatchHistoryReportRow(
        Guid ShipmentId,
        string ShipmentNumber,
        string OrderNumber,
        string? CustomerName,
        string Carrier,
        string? TrackingNumber,
        DateTime DispatchDate,
        DateTime? DeliveryDate,
        DateTime? RequestedDeliveryDate,
        string Status);

    public sealed record DispatchHistoryReportResponse(
        DispatchHistorySummary Summary,
        IReadOnlyList<DispatchHistoryReportRow> Shipments,
        int TotalCount,
        int Page,
        int PageSize);

    public sealed record StockMovementRow(
        DateTimeOffset Timestamp,
        string MovementType,
        int ItemId,
        string ItemSku,
        string ItemName,
        int? FromLocationId,
        string? FromLocationCode,
        int? ToLocationId,
        string? ToLocationCode,
        decimal Qty,
        string BaseUoM,
        string? Operator,
        string? Reason,
        string Reference);

    public sealed record StockMovementsResponse(
        IReadOnlyList<StockMovementRow> Movements,
        int TotalCount,
        int Page,
        int PageSize);

    public sealed record TraceabilityLot(string LotNumber, string ItemSku, string ItemName);
    public sealed record TraceabilityUpstream(string Supplier, DateTime ReceiptDate, string InboundShipment, decimal QtyReceived);
    public sealed record TraceabilityCurrent(string? Location, decimal AvailableQty);
    public sealed record TraceabilityProductionOrder(string OrderNumber, decimal QtyIssued, DateTime? IssuedDate);
    public sealed record TraceabilitySalesOrder(string OrderNumber, string Customer, decimal QtyShipped, DateTime? ShippedDate, string? TrackingNumber);
    public sealed record TraceabilityDownstream(
        IReadOnlyList<TraceabilityProductionOrder> ProductionOrders,
        IReadOnlyList<TraceabilitySalesOrder> SalesOrders);
    public sealed record TraceabilityEntryResponse(
        TraceabilityLot Lot,
        TraceabilityUpstream? Upstream,
        TraceabilityCurrent Current,
        TraceabilityDownstream Downstream,
        bool IsApproximate);
    public sealed record TraceabilityResponse(IReadOnlyList<TraceabilityEntryResponse> Entries);

    public sealed record ComplianceAuditRow(
        DateTimeOffset Timestamp,
        string ReportType,
        string Actor,
        string Reference,
        string Category,
        string Details,
        string? Notes);

    public sealed record ComplianceAuditResponse(
        IReadOnlyList<ComplianceAuditRow> Rows,
        int TotalCount,
        int Page,
        int PageSize);

    private static class SimplePdfBuilder
    {
        public static byte[] BuildSinglePage(string title, IReadOnlyList<string> lines)
        {
            using var stream = new MemoryStream();

            static void WriteLine(MemoryStream ms, string text)
            {
                var bytes = Encoding.ASCII.GetBytes(text + "\n");
                ms.Write(bytes, 0, bytes.Length);
            }

            static string Escape(string value)
                => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

            var contentBuilder = new StringBuilder();
            contentBuilder.Append("BT /F1 11 Tf 40 800 Td ");
            contentBuilder.Append($"({Escape(title)}) Tj 0 -18 Td ");
            foreach (var line in lines.Take(42))
            {
                contentBuilder.Append($"({Escape(line)}) Tj 0 -14 Td ");
            }

            contentBuilder.Append("ET");
            var content = Encoding.ASCII.GetBytes(contentBuilder.ToString());

            WriteLine(stream, "%PDF-1.4");

            var offsets = new Dictionary<int, long>();

            offsets[1] = stream.Position;
            WriteLine(stream, "1 0 obj");
            WriteLine(stream, "<< /Type /Catalog /Pages 2 0 R >>");
            WriteLine(stream, "endobj");

            offsets[2] = stream.Position;
            WriteLine(stream, "2 0 obj");
            WriteLine(stream, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
            WriteLine(stream, "endobj");

            offsets[3] = stream.Position;
            WriteLine(stream, "3 0 obj");
            WriteLine(stream, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>");
            WriteLine(stream, "endobj");

            offsets[4] = stream.Position;
            WriteLine(stream, "4 0 obj");
            WriteLine(stream, $"<< /Length {content.Length} >>");
            WriteLine(stream, "stream");
            stream.Write(content, 0, content.Length);
            WriteLine(stream, string.Empty);
            WriteLine(stream, "endstream");
            WriteLine(stream, "endobj");

            offsets[5] = stream.Position;
            WriteLine(stream, "5 0 obj");
            WriteLine(stream, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
            WriteLine(stream, "endobj");

            var xrefPosition = stream.Position;
            WriteLine(stream, "xref");
            WriteLine(stream, "0 6");
            WriteLine(stream, "0000000000 65535 f ");
            for (var i = 1; i <= 5; i++)
            {
                WriteLine(stream, $"{offsets[i]:D10} 00000 n ");
            }

            WriteLine(stream, "trailer");
            WriteLine(stream, "<< /Size 6 /Root 1 0 R >>");
            WriteLine(stream, "startxref");
            WriteLine(stream, xrefPosition.ToString(CultureInfo.InvariantCulture));
            WriteLine(stream, "%%EOF");

            return stream.ToArray();
        }
    }
}
