using System.Globalization;
using System.Text;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Api.Configuration;
using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using IDocumentStore = Marten.IDocumentStore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

/// <summary>
/// Stock by supplier: for every item a supplier provides, what is on hand, what was issued over the last
/// months, what is ordered and still expected, the prices and what is missing to reach the item minimum.
/// </summary>
[ApiController]
[Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
[Route("api/warehouse/v1/reports/supplier-stock")]
public sealed class SupplierReportsController : ControllerBase
{
    private static readonly string[] OpenStatuses = ["Draft", "Partial"];

    private readonly WarehouseDbContext _dbContext;
    private readonly IDocumentStore _documentStore;
    private readonly IItemPriceSnapshotService _prices;
    private readonly IWarehouseCodeProvider _warehouseCode;

    public SupplierReportsController(WarehouseDbContext dbContext, IDocumentStore documentStore,
        IItemPriceSnapshotService prices, IWarehouseCodeProvider warehouseCode)
    {
        _dbContext = dbContext;
        _documentStore = documentStore;
        _prices = prices;
        _warehouseCode = warehouseCode;
    }

    [HttpGet]
    public async Task<IActionResult> GetAsync(
        [FromQuery] int supplierId,
        [FromQuery] int months = 3,
        [FromQuery] bool onlyShortage = false,
        [FromQuery] bool exportCsv = false,
        CancellationToken cancellationToken = default)
    {
        months = Math.Clamp(months, 1, 36);
        var supplier = await _dbContext.Suppliers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == supplierId, cancellationToken);
        if (supplier is null)
        {
            var problem = ResultProblemDetailsMapper.ToProblemDetails(Result.Fail(DomainErrorCodes.NotFound, $"Supplier '{supplierId}' does not exist."), HttpContext);
            return new ObjectResult(problem) { StatusCode = problem.Status };
        }

        var since = DateTime.UtcNow.AddMonths(-months);

        // The supplier's items: those mapped to it, and those it has ever been invoiced for.
        var mappings = await _dbContext.SupplierItemMappings.AsNoTracking()
            .Where(x => x.SupplierId == supplierId)
            .Select(x => new { x.ItemId, x.SupplierSKU, x.LeadTimeDays, x.MinOrderQty, x.PricePerUnit })
            .ToListAsync(cancellationToken);
        var lines = await _dbContext.InboundShipmentLines.AsNoTracking()
            .Where(x => x.Shipment != null && x.Shipment.SupplierId == supplierId && x.Shipment.Status != "Cancelled")
            .Select(x => new
            {
                x.ItemId, x.ExpectedQty, x.ReceivedQty, x.UnitPrice, x.InvoiceUnitPrice, x.Currency,
                x.Shipment!.Status, x.Shipment.ExpectedDate, x.Shipment.InvoiceDate, x.Shipment.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var itemIds = mappings.Select(x => x.ItemId).Concat(lines.Select(x => x.ItemId)).Distinct().ToList();
        var items = await _dbContext.Items.AsNoTracking()
            .Where(x => itemIds.Contains(x.Id) && x.ItemType != ItemType.Service)
            .ToListAsync(cancellationToken);
        if (items.Count == 0)
        {
            return Respond(supplier, months, [], exportCsv);
        }

        var settings = await _dbContext.WarehouseSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var physical = (await _dbContext.Locations.AsNoTracking().ToListAsync(cancellationToken))
            .Where(PhysicalLocation.IsPhysical).Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        await using var session = _documentStore.QuerySession();
        var warehouse = _warehouseCode.Code;
        var onHandBySku = (await CommittedLocationStock.ReadBalancesAsync(session, null, cancellationToken))
            .Where(x => string.Equals(x.Warehouse, warehouse, StringComparison.OrdinalIgnoreCase) && physical.Contains(x.Location))
            .GroupBy(x => x.Sku, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Sum(b => b.Quantity), StringComparer.OrdinalIgnoreCase);

        // Issued in the period: movements out of the warehouse, by where they went.
        var issued = new Dictionary<string, decimal[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var movement in await CommittedEventData.ReadAsync<StockMovedEvent>(session, null, cancellationToken))
        {
            if (movement.Timestamp < since || !physical.Contains(movement.FromLocation)) continue;
            if (SupplierStockMath.KindOf(movement.ToLocation) is not { } kind) continue;
            if (!issued.TryGetValue(movement.SKU, out var totals)) issued[movement.SKU] = totals = new decimal[3];
            totals[(int)kind] += movement.Quantity;
        }

        var prices = (await _prices.GetAsync(items.Select(x => x.Id).ToArray(), Array.Empty<string>(), cancellationToken))
            .ToDictionary(x => x.ItemId);
        var mappingByItem = mappings.GroupBy(x => x.ItemId).ToDictionary(x => x.Key, x => x.OrderBy(m => m.SupplierSKU, StringComparer.OrdinalIgnoreCase).First());
        var linesByItem = lines.ToLookup(x => x.ItemId);

        var rows = items.Select(item =>
        {
            mappingByItem.TryGetValue(item.Id, out var mapping);
            var itemLines = linesByItem[item.Id].ToList();
            var open = itemLines.Where(x => OpenStatuses.Contains(x.Status)).ToList();
            var openQty = open.Sum(x => SupplierStockMath.OpenQty(x.ExpectedQty, x.ReceivedQty));
            var nextExpected = open.Where(x => x.ExpectedDate.HasValue && SupplierStockMath.OpenQty(x.ExpectedQty, x.ReceivedQty) > 0m)
                .Select(x => x.ExpectedDate).Min();
            var lastInvoice = itemLines.Where(x => (x.InvoiceUnitPrice ?? x.UnitPrice).HasValue)
                .OrderByDescending(x => x.InvoiceDate.HasValue ? x.InvoiceDate.Value.ToDateTime(TimeOnly.MinValue) : x.CreatedAt.UtcDateTime)
                .FirstOrDefault();
            onHandBySku.TryGetValue(item.InternalSKU, out var onHand);
            issued.TryGetValue(item.InternalSKU, out var totals);
            totals ??= new decimal[3];
            var issuedTotal = totals.Sum();
            prices.TryGetValue(item.Id, out var price);
            var minQty = item.MinQty ?? (settings is null ? null : settings.ReorderPoint);
            return new SupplierStockRow(
                item.Id, item.InternalSKU, item.Name, item.BaseUoM, mapping?.SupplierSKU,
                onHand,
                totals[(int)SupplierStockIssueKind.Production], totals[(int)SupplierStockIssueKind.Dispatch], totals[(int)SupplierStockIssueKind.WriteOff],
                issuedTotal, SupplierStockMath.AveragePerMonth(issuedTotal, months),
                openQty, nextExpected,
                minQty, item.MinQty.HasValue, SupplierStockMath.Shortage(minQty, onHand, openQty),
                item.PurchasePrice, mapping?.PricePerUnit,
                lastInvoice is null ? null : lastInvoice.InvoiceUnitPrice ?? lastInvoice.UnitPrice,
                lastInvoice?.Currency, lastInvoice?.InvoiceDate,
                price?.EffectiveCost, mapping?.LeadTimeDays, mapping?.MinOrderQty);
        })
        .Where(x => !onlyShortage || x.Shortage > 0m)
        .OrderByDescending(x => x.Shortage ?? 0m)
        .ThenBy(x => x.ItemSku, StringComparer.OrdinalIgnoreCase)
        .ToList();

        return Respond(supplier, months, rows, exportCsv);
    }

    private IActionResult Respond(Supplier supplier, int months, IReadOnlyList<SupplierStockRow> rows, bool exportCsv)
    {
        if (!exportCsv)
        {
            return Ok(new SupplierStockReport(supplier.Id, supplier.Code, supplier.Name, months, DateTimeOffset.UtcNow, rows));
        }

        static string N(decimal? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        static string T(string? value) => value is null ? string.Empty : value.Contains(',') || value.Contains('"') ? '"' + value.Replace("\"", "\"\"") + '"' : value;
        var csv = new StringBuilder();
        csv.AppendLine("ItemSku,ItemName,UoM,SupplierCode,OnHand,IssuedToProduction,Dispatched,WrittenOff,IssuedTotal,AveragePerMonth,OpenInboundQty,NextExpectedDate,MinQty,Shortage,PurchasePrice,SupplierPrice,LastInvoicePrice,LastInvoiceCurrency,LastInvoiceDate,UnitCost,LeadTimeDays,MinOrderQty");
        foreach (var r in rows)
        {
            csv.AppendLine(string.Join(',', T(r.ItemSku), T(r.ItemName), T(r.BaseUoM), T(r.SupplierSku), N(r.OnHand), N(r.IssuedToProduction), N(r.Dispatched),
                N(r.WrittenOff), N(r.IssuedTotal), N(r.AveragePerMonth), N(r.OpenInboundQty), r.NextExpectedDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                N(r.MinQty), N(r.Shortage), N(r.PurchasePrice), N(r.SupplierPrice), N(r.LastInvoicePrice), T(r.LastInvoiceCurrency),
                r.LastInvoiceDate?.ToString("yyyy-MM-dd") ?? string.Empty, N(r.UnitCost), r.LeadTimeDays?.ToString(CultureInfo.InvariantCulture) ?? string.Empty, N(r.MinOrderQty)));
        }

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"supplier-stock-{supplier.Code}-{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }

    public sealed record SupplierStockReport(
        int SupplierId, string SupplierCode, string SupplierName, int Months, DateTimeOffset GeneratedAt, IReadOnlyList<SupplierStockRow> Rows);

    public sealed record SupplierStockRow(
        int ItemId,
        string ItemSku,
        string ItemName,
        string BaseUoM,
        string? SupplierSku,
        decimal OnHand,
        decimal IssuedToProduction,
        decimal Dispatched,
        decimal WrittenOff,
        decimal IssuedTotal,
        decimal AveragePerMonth,
        decimal OpenInboundQty,
        DateOnly? NextExpectedDate,
        decimal? MinQty,
        bool MinQtyIsItemOwn,
        decimal? Shortage,
        decimal? PurchasePrice,
        decimal? SupplierPrice,
        decimal? LastInvoicePrice,
        string? LastInvoiceCurrency,
        DateOnly? LastInvoiceDate,
        decimal? UnitCost,
        int? LeadTimeDays,
        decimal? MinOrderQty);
}
