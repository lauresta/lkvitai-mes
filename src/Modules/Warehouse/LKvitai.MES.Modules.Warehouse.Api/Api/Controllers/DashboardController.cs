using System.Reflection;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private const string DefaultWarehouseId = "WH1";

    private readonly IDocumentStore _documentStore;
    private readonly WarehouseDbContext _dbContext;
    private readonly IProjectionHealthService _projectionHealthService;
    private readonly IWarehouseSettingsService _warehouseSettingsService;

    public DashboardController(
        IDocumentStore documentStore,
        WarehouseDbContext dbContext,
        IProjectionHealthService projectionHealthService,
        IWarehouseSettingsService warehouseSettingsService)
    {
        _documentStore = documentStore;
        _dbContext = dbContext;
        _projectionHealthService = projectionHealthService;
        _warehouseSettingsService = warehouseSettingsService;
    }

    [HttpGet("health")]
    public async Task<ActionResult<HealthStatusDto>> GetHealthAsync(CancellationToken cancellationToken)
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "dev";
        var snapshot = await _projectionHealthService.GetHealthAsync(cancellationToken);

        var normalizedProjectionRows = snapshot.ProjectionStatus
            .Values
            .Select(x => new
            {
                LagEvents = x.LagEvents,
                LagSeconds = x.LagEvents == 0 ? 0d : x.LagSeconds,
                Status = x.LagEvents == 0 ? "Healthy" : x.Status
            })
            .ToList();

        var projectionLag = normalizedProjectionRows
            .Where(x => x.LagSeconds.HasValue)
            .Select(x => x.LagSeconds!.Value)
            .DefaultIfEmpty()
            .Max();
        var hasLag = normalizedProjectionRows.Any(x => x.LagSeconds.HasValue);
        var projectionLagStatus = normalizedProjectionRows.Count == 0
            ? "Healthy"
            : normalizedProjectionRows.Any(x => x.Status == "Unhealthy")
                ? "Unhealthy"
                : normalizedProjectionRows.Any(x => x.Status == "Degraded")
                    ? "Degraded"
                    : "Healthy";
        var overallStatus = snapshot.DatabaseStatus == "Unhealthy" || snapshot.EventStoreStatus == "Unhealthy" || projectionLagStatus == "Unhealthy"
            ? "Degraded"
            : projectionLagStatus == "Degraded"
                ? "Degraded"
                : "Healthy";

        var response = new HealthStatusDto
        {
            Ok = !string.Equals(overallStatus, "Unhealthy", StringComparison.OrdinalIgnoreCase),
            Service = "LKvitai.MES.Modules.Warehouse.Api",
            Version = version,
            UtcNow = DateTime.UtcNow,
            Status = overallStatus,
            ProjectionLag = hasLag ? projectionLag : null,
            LastCheck = snapshot.CheckedAt.UtcDateTime
        };

        return Ok(response);
    }

    [HttpGet("projection-health")]
    public async Task<ActionResult<IReadOnlyList<ProjectionHealthDto>>> GetProjectionHealthAsync(CancellationToken cancellationToken)
    {
        var snapshot = await _projectionHealthService.GetHealthAsync(cancellationToken);
        var projections = snapshot.ProjectionStatus
            .Values
            .Select(x => new ProjectionHealthDto
            {
                ProjectionName = x.ProjectionName,
                HighWaterMark = x.HighWaterMark,
                LastProcessed = x.LastProcessed,
                LagSeconds = x.LagEvents == 0 ? 0d : x.LagSeconds,
                LagEvents = x.LagEvents,
                LastUpdated = x.LastUpdated,
                Status = x.LagEvents == 0 ? "Healthy" : x.Status
            })
            .OrderBy(projection => projection.ProjectionName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(projections);
    }

    [HttpGet("stock-summary")]
    public async Task<ActionResult<StockSummaryDto>> GetStockSummaryAsync(CancellationToken cancellationToken)
    {
        await using var querySession = _documentStore.QuerySession();
        var stockRows = await Marten.QueryableExtensions.ToListAsync(
            querySession.Query<AvailableStockView>()
                .Where(x => x.WarehouseId == DefaultWarehouseId && x.OnHandQty > 0m),
            cancellationToken);

        var totalSkus = stockRows
            .Select(x => x.SKU)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        var totalQuantity = stockRows.Sum(x => x.OnHandQty);

        if (stockRows.Count == 0)
        {
            return Ok(new StockSummaryDto());
        }

        var skuList = stockRows
            .Select(x => x.SKU)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var items = _dbContext.Items
            .AsNoTracking()
            .Where(x => skuList.Contains(x.InternalSKU))
            .Select(x => new { x.Id, x.InternalSKU });
        var itemList = await EntityFrameworkQueryableExtensions.ToListAsync(items, cancellationToken);

        var itemIdBySku = itemList
            .GroupBy(x => x.InternalSKU, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var itemIds = itemList.Select(x => x.Id).Distinct().ToList();
        var minPriceByItemId = !itemIds.Any()
            ? new Dictionary<int, decimal>()
            : await _dbContext.SupplierItemMappings
                .AsNoTracking()
                .Where(x => itemIds.Contains(x.ItemId) && x.PricePerUnit.HasValue)
                .GroupBy(x => x.ItemId)
                .Select(group => new { ItemId = group.Key, UnitPrice = group.Min(x => x.PricePerUnit) })
                .ToDictionaryAsync(x => x.ItemId, x => x.UnitPrice ?? 0m, cancellationToken);

        decimal totalValue = 0m;
        foreach (var row in stockRows)
        {
            if (!itemIdBySku.TryGetValue(row.SKU, out var itemId))
            {
                continue;
            }

            if (!minPriceByItemId.TryGetValue(itemId, out var unitPrice))
            {
                continue;
            }

            totalValue += row.OnHandQty * unitPrice;
        }

        return Ok(new StockSummaryDto
        {
            TotalSKUs = totalSkus,
            TotalQuantity = totalQuantity,
            TotalValue = decimal.Round(totalValue, 2, MidpointRounding.AwayFromZero)
        });
    }

    [HttpGet("reservation-summary")]
    public async Task<ActionResult<ReservationSummaryDto>> GetReservationSummaryAsync(CancellationToken cancellationToken)
    {
        await using var querySession = _documentStore.QuerySession();
        var allReservations = querySession.Query<ReservationSummaryView>();

        var allocated = await Marten.QueryableExtensions.CountAsync(
            allReservations.Where(x => x.Status == "ALLOCATED"),
            cancellationToken);
        var picking = await Marten.QueryableExtensions.CountAsync(
            allReservations.Where(x => x.Status == "PICKING"),
            cancellationToken);
        var consumed = await Marten.QueryableExtensions.CountAsync(
            allReservations.Where(x => x.Status == "CONSUMED"),
            cancellationToken);

        return Ok(new ReservationSummaryDto
        {
            Allocated = allocated,
            Picking = picking,
            Consumed = consumed
        });
    }

    [HttpGet("recent-activity")]
    public async Task<ActionResult<IReadOnlyList<RecentMovementDto>>> GetRecentActivityAsync(
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var normalizedLimit = Math.Clamp(limit, 1, 100);
        var queryLimit = normalizedLimit * 3;

        var transferRows = await EntityFrameworkQueryableExtensions.ToListAsync(
            _dbContext.TransferLines
                .AsNoTracking()
                .Where(x => x.Transfer != null && (x.Transfer.ExecutedAt != null || x.Transfer.CompletedAt != null))
                .OrderByDescending(x => x.Transfer!.CompletedAt ?? x.Transfer.ExecutedAt ?? x.Transfer.RequestedAt)
                .Select(x => new
                {
                    x.Id,
                    x.ItemId,
                    Sku = x.Item != null ? x.Item.InternalSKU : null,
                    x.Qty,
                    x.FromLocationId,
                    FromLocationCode = x.FromLocation != null ? x.FromLocation.Code : null,
                    x.ToLocationId,
                    ToLocationCode = x.ToLocation != null ? x.ToLocation.Code : null,
                    Timestamp = x.Transfer!.CompletedAt ?? x.Transfer.ExecutedAt ?? x.Transfer.RequestedAt
                })
                .Take(queryLimit),
            cancellationToken);

        var transferMovements = transferRows.Select(x => new RecentMovementDto
        {
            MovementId = x.Id,
            SKU = string.IsNullOrWhiteSpace(x.Sku) ? $"ITEM-{x.ItemId}" : x.Sku!,
            Quantity = x.Qty,
            FromLocation = string.IsNullOrWhiteSpace(x.FromLocationCode)
                ? x.FromLocationId.ToString()
                : x.FromLocationCode!,
            ToLocation = string.IsNullOrWhiteSpace(x.ToLocationCode)
                ? x.ToLocationId.ToString()
                : x.ToLocationCode!,
            Timestamp = x.Timestamp.UtcDateTime
        });

        await using var querySession = _documentStore.QuerySession();
        var rows = await Marten.QueryableExtensions.ToListAsync(
            querySession.Query<AdjustmentHistoryView>()
                .OrderByDescending(x => x.Timestamp)
                .Take(queryLimit),
            cancellationToken);

        var adjustmentMovements = rows.Select(x =>
        {
            var location = x.LocationCode ?? x.Location;
            return new RecentMovementDto
            {
                MovementId = x.AdjustmentId,
                SKU = x.SKU,
                Quantity = x.QtyDelta,
                FromLocation = x.QtyDelta < 0m ? location : "-",
                ToLocation = x.QtyDelta > 0m ? location : "-",
                Timestamp = x.Timestamp.UtcDateTime
            };
        });

        var items = transferMovements
            .Concat(adjustmentMovements)
            .OrderByDescending(x => x.Timestamp)
            .Take(normalizedLimit)
            .ToList();

        return Ok(items);
    }

    [HttpGet("warehouses")]
    public async Task<ActionResult<IReadOnlyList<WarehouseOptionDto>>> GetWarehousesAsync(CancellationToken cancellationToken)
    {
        var warehousesQuery = _dbContext.Warehouses
            .AsNoTracking()
            .Where(x => !x.IsVirtual && x.Status == "Active")
            .OrderBy(x => x.Code)
            .Select(x => new WarehouseOptionDto(x.Code, x.Name));
        var warehouses = await EntityFrameworkQueryableExtensions.ToListAsync(warehousesQuery, cancellationToken);

        return Ok(warehouses);
    }

    [HttpGet("overview")]
    public async Task<ActionResult<DashboardOverviewDto>> GetOverviewAsync(
        [FromQuery] string? warehouseId,
        CancellationToken cancellationToken)
    {
        var settings = await _warehouseSettingsService.GetAsync(cancellationToken);

        await using var querySession = _documentStore.QuerySession();
        IQueryable<AvailableStockView> stockQuery = querySession.Query<AvailableStockView>().Where(x => x.OnHandQty > 0m);
        if (!string.IsNullOrWhiteSpace(warehouseId))
        {
            stockQuery = stockQuery.Where(x => x.WarehouseId == warehouseId);
        }

        var stockRows = await Marten.QueryableExtensions.ToListAsync(stockQuery, cancellationToken);

        var bySku = stockRows
            .GroupBy(x => x.SKU, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { SKU = g.Key, OnHandQty = g.Sum(x => x.OnHandQty) })
            .ToList();

        var unitCostBySku = await _dbContext.OnHandValues
            .AsNoTracking()
            .ToDictionaryAsync(x => x.ItemSku, x => x.UnitCost, StringComparer.OrdinalIgnoreCase, cancellationToken);

        decimal totalValue = 0m;
        var lowStockCount = 0;
        var outOfStockCount = 0;

        foreach (var row in bySku)
        {
            if (unitCostBySku.TryGetValue(row.SKU, out var unitCost))
            {
                totalValue += row.OnHandQty * unitCost;
            }

            if (row.OnHandQty <= 0m)
            {
                outOfStockCount++;
            }
            else if (row.OnHandQty <= settings.ReorderPoint)
            {
                lowStockCount++;
            }
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expiringCutoff = today.AddDays(30);
        var expiringSoonCount = stockRows.Count(x => x.ExpiryDate.HasValue && x.ExpiryDate.Value >= today && x.ExpiryDate.Value <= expiringCutoff);
        var expiredCount = stockRows.Count(x => x.ExpiryDate.HasValue && x.ExpiryDate.Value < today);

        var agnumHealth = await BuildAgnumHealthAsync(cancellationToken);

        return Ok(new DashboardOverviewDto
        {
            TotalStockValue = decimal.Round(totalValue, 2, MidpointRounding.AwayFromZero),
            TotalSKUs = bySku.Count(x => x.OnHandQty > 0m),
            TotalQuantity = bySku.Sum(x => x.OnHandQty),
            LowStockCount = lowStockCount,
            OutOfStockCount = outOfStockCount,
            ExpiringSoonCount = expiringSoonCount,
            ExpiredCount = expiredCount,
            AgnumStatus = agnumHealth.OverallStatus
        });
    }

    [HttpGet("stock-by-category")]
    public async Task<ActionResult<IReadOnlyList<CategoryValueDto>>> GetStockByCategoryAsync(
        [FromQuery] string? warehouseId,
        CancellationToken cancellationToken)
    {
        List<(string Category, decimal Value)> rows;

        if (string.IsNullOrWhiteSpace(warehouseId))
        {
            var groupedQuery = _dbContext.OnHandValues
                .AsNoTracking()
                .GroupBy(x => x.CategoryName ?? "Uncategorized")
                .Select(g => new { Category = g.Key, Value = g.Sum(x => x.TotalValue) });
            var grouped = await EntityFrameworkQueryableExtensions.ToListAsync(groupedQuery, cancellationToken);

            rows = grouped.Select(x => (x.Category, x.Value)).ToList();
        }
        else
        {
            await using var querySession = _documentStore.QuerySession();
            var stockRows = await Marten.QueryableExtensions.ToListAsync(
                querySession.Query<AvailableStockView>().Where(x => x.WarehouseId == warehouseId && x.OnHandQty > 0m),
                cancellationToken);

            var qtyBySku = stockRows
                .GroupBy(x => x.SKU, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.OnHandQty), StringComparer.OrdinalIgnoreCase);

            var skuList = qtyBySku.Keys.ToList();
            var valuationsQuery = _dbContext.OnHandValues
                .AsNoTracking()
                .Where(x => skuList.Contains(x.ItemSku));
            var valuations = await EntityFrameworkQueryableExtensions.ToListAsync(valuationsQuery, cancellationToken);

            rows = valuations
                .GroupBy(x => x.CategoryName ?? "Uncategorized")
                .Select(g => (g.Key, g.Sum(x => qtyBySku.GetValueOrDefault(x.ItemSku) * x.UnitCost)))
                .ToList();
        }

        var total = rows.Sum(x => x.Value);
        var ordered = rows.Where(x => x.Value > 0m).OrderByDescending(x => x.Value).ToList();

        const int topCategoryCount = 5;
        var top = ordered.Take(topCategoryCount).ToList();
        var otherValue = ordered.Skip(topCategoryCount).Sum(x => x.Value);
        if (otherValue > 0m)
        {
            top.Add(("Other", otherValue));
        }

        var result = top
            .Select(x => new CategoryValueDto(
                x.Category,
                decimal.Round(x.Value, 2, MidpointRounding.AwayFromZero),
                total == 0m ? 0m : decimal.Round(x.Value / total * 100m, 1, MidpointRounding.AwayFromZero)))
            .ToList();

        return Ok(result);
    }

    [HttpGet("stock-by-warehouse")]
    public async Task<ActionResult<IReadOnlyList<WarehouseValueDto>>> GetStockByWarehouseAsync(CancellationToken cancellationToken)
    {
        var warehousesQuery = _dbContext.Warehouses
            .AsNoTracking()
            .Where(x => !x.IsVirtual && x.Status == "Active")
            .Select(x => new { x.Code, x.Name });
        var warehouses = await EntityFrameworkQueryableExtensions.ToListAsync(warehousesQuery, cancellationToken);

        await using var querySession = _documentStore.QuerySession();
        var stockRows = await Marten.QueryableExtensions.ToListAsync(
            querySession.Query<AvailableStockView>().Where(x => x.OnHandQty > 0m),
            cancellationToken);

        var unitCostBySku = await _dbContext.OnHandValues
            .AsNoTracking()
            .ToDictionaryAsync(x => x.ItemSku, x => x.UnitCost, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var byWarehouse = stockRows
            .GroupBy(x => x.WarehouseId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (
                    Value: g.Sum(x => (unitCostBySku.TryGetValue(x.SKU, out var cost) ? cost : 0m) * x.OnHandQty),
                    Qty: g.Sum(x => x.OnHandQty)),
                StringComparer.OrdinalIgnoreCase);

        var result = warehouses
            .Select(w =>
            {
                var (value, qty) = byWarehouse.GetValueOrDefault(w.Code, (0m, 0m));
                return new WarehouseValueDto(w.Code, w.Name, decimal.Round(value, 2, MidpointRounding.AwayFromZero), qty);
            })
            .OrderByDescending(x => x.Value)
            .ToList();

        return Ok(result);
    }

    [HttpGet("low-stock")]
    public async Task<ActionResult<LowStockResponseDto>> GetLowStockAsync(
        [FromQuery] string? warehouseId,
        [FromQuery] int take = 5,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 100);
        var settings = await _warehouseSettingsService.GetAsync(cancellationToken);

        await using var querySession = _documentStore.QuerySession();
        IQueryable<AvailableStockView> stockQuery = querySession.Query<AvailableStockView>();
        if (!string.IsNullOrWhiteSpace(warehouseId))
        {
            stockQuery = stockQuery.Where(x => x.WarehouseId == warehouseId);
        }

        var rows = await Marten.QueryableExtensions.ToListAsync(stockQuery, cancellationToken);

        var bySku = rows
            .GroupBy(x => x.SKU, StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                SKU = g.Key,
                OnHandQty = g.Sum(x => x.OnHandQty),
                AvailableQty = g.Sum(x => x.AvailableQty)
            })
            .Where(x => x.OnHandQty <= settings.ReorderPoint)
            .ToList();

        if (bySku.Count == 0)
        {
            return Ok(new LowStockResponseDto(Array.Empty<LowStockItemDto>(), 0));
        }

        var skuList = bySku.Select(x => x.SKU).ToList();
        var itemsQuery = _dbContext.Items
            .AsNoTracking()
            .Where(x => skuList.Contains(x.InternalSKU))
            .Select(x => new { x.Id, x.InternalSKU, x.Name, x.CategoryId, x.BaseUoM });
        var items = await EntityFrameworkQueryableExtensions.ToListAsync(itemsQuery, cancellationToken);

        var itemBySku = items
            .GroupBy(x => x.InternalSKU, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var categoryIds = itemBySku.Values.Select(x => x.CategoryId).Distinct().ToList();
        var categoryNameById = await _dbContext.ItemCategories
            .AsNoTracking()
            .Where(x => categoryIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var itemIds = itemBySku.Values.Select(x => x.Id).Distinct().ToList();
        var supplierMappingsQuery = _dbContext.SupplierItemMappings
            .AsNoTracking()
            .Where(x => itemIds.Contains(x.ItemId));
        var supplierMappings = await EntityFrameworkQueryableExtensions.ToListAsync(supplierMappingsQuery, cancellationToken);

        var supplierIds = supplierMappings.Select(x => x.SupplierId).Distinct().ToList();
        var supplierNameById = await _dbContext.Suppliers
            .AsNoTracking()
            .Where(x => supplierIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var bestSupplierByItemId = supplierMappings
            .GroupBy(x => x.ItemId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.LeadTimeDays ?? int.MaxValue).First());

        var dtos = bySku
            .Where(x => itemBySku.ContainsKey(x.SKU))
            .Select(x =>
            {
                var item = itemBySku[x.SKU];
                var categoryName = categoryNameById.GetValueOrDefault(item.CategoryId, "-");
                bestSupplierByItemId.TryGetValue(item.Id, out var supplierMapping);
                var supplierName = supplierMapping is not null
                    ? supplierNameById.GetValueOrDefault(supplierMapping.SupplierId)
                    : null;

                return new LowStockItemDto(
                    x.SKU,
                    item.Name,
                    categoryName,
                    item.BaseUoM,
                    x.OnHandQty,
                    x.AvailableQty,
                    settings.ReorderPoint,
                    supplierName,
                    supplierMapping?.LeadTimeDays,
                    x.OnHandQty <= 0m ? "Out" : "Low");
            })
            .OrderBy(x => x.OnHandQty)
            .ToList();

        return Ok(new LowStockResponseDto(dtos.Take(take).ToList(), dtos.Count));
    }

    [HttpGet("expiring")]
    public async Task<ActionResult<IReadOnlyList<ExpiringLotDto>>> GetExpiringAsync(
        [FromQuery] string? warehouseId,
        [FromQuery] int days = 90,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 200);
        days = Math.Clamp(days, 1, 365);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cutoff = today.AddDays(days);

        await using var querySession = _documentStore.QuerySession();
        IQueryable<AvailableStockView> query = querySession.Query<AvailableStockView>()
            .Where(x => x.OnHandQty > 0m && x.ExpiryDate != null && x.ExpiryDate <= cutoff);
        if (!string.IsNullOrWhiteSpace(warehouseId))
        {
            query = query.Where(x => x.WarehouseId == warehouseId);
        }

        var rows = await Marten.QueryableExtensions.ToListAsync(query, cancellationToken);

        var skuList = rows.Select(x => x.SKU).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var itemNameBySku = await _dbContext.Items
            .AsNoTracking()
            .Where(x => skuList.Contains(x.InternalSKU))
            .ToDictionaryAsync(x => x.InternalSKU, x => x.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var dtos = rows
            .OrderBy(x => x.ExpiryDate)
            .Take(take)
            .Select(x =>
            {
                var daysRemaining = x.ExpiryDate!.Value.DayNumber - today.DayNumber;
                return new ExpiringLotDto(
                    x.SKU,
                    itemNameBySku.GetValueOrDefault(x.SKU, x.ItemName ?? x.SKU),
                    x.LotNumber,
                    x.LocationCode ?? x.Location,
                    x.OnHandQty,
                    x.BaseUoM ?? string.Empty,
                    x.ExpiryDate.Value,
                    daysRemaining,
                    BucketFor(daysRemaining));
            })
            .ToList();

        return Ok(dtos);
    }

    [HttpGet("incoming")]
    public async Task<ActionResult<IReadOnlyList<IncomingShipmentDto>>> GetIncomingAsync(
        [FromQuery] int take = 8,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 50);

        await using var querySession = _documentStore.QuerySession();
        var rows = await Marten.QueryableExtensions.ToListAsync(
            querySession.Query<InboundShipmentSummaryView>().Where(x => x.CompletionPercent < 100m),
            cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dtos = rows
            .OrderBy(x => x.ExpectedDate ?? DateOnly.MaxValue)
            .Take(take)
            .Select(x => new IncomingShipmentDto(
                x.ReferenceNumber,
                x.SupplierName,
                x.TotalLines,
                x.ExpectedDate,
                x.Status,
                x.TotalReceivedQty,
                x.TotalExpectedQty,
                x.CompletionPercent,
                x.ExpectedDate.HasValue && x.ExpectedDate.Value < today))
            .ToList();

        return Ok(dtos);
    }

    [HttpGet("agnum-health")]
    public async Task<ActionResult<AgnumHealthDto>> GetAgnumHealthAsync(CancellationToken cancellationToken)
    {
        var dto = await BuildAgnumHealthAsync(cancellationToken);
        return Ok(dto);
    }

    [HttpGet("reservation-funnel")]
    public async Task<ActionResult<ReservationFunnelDto>> GetReservationFunnelAsync(CancellationToken cancellationToken)
    {
        await using var querySession = _documentStore.QuerySession();
        var reservations = querySession.Query<ReservationSummaryView>();

        var allocated = await Marten.QueryableExtensions.CountAsync(reservations.Where(x => x.Status == "ALLOCATED"), cancellationToken);
        var picking = await Marten.QueryableExtensions.CountAsync(reservations.Where(x => x.Status == "PICKING"), cancellationToken);
        var consumed = await Marten.QueryableExtensions.CountAsync(reservations.Where(x => x.Status == "CONSUMED"), cancellationToken);

        var cutoff = DateTime.UtcNow.AddHours(-2);
        var stuck = await Marten.QueryableExtensions.CountAsync(
            reservations.Where(x => x.Status == "PICKING" && x.PickingStartedAt != null && x.PickingStartedAt < cutoff),
            cancellationToken);

        var activeHardLocks = await Marten.QueryableExtensions.CountAsync(
            querySession.Query<ActiveHardLockView>(),
            cancellationToken);

        return Ok(new ReservationFunnelDto
        {
            Allocated = allocated,
            Picking = picking,
            Consumed = consumed,
            ActiveHardLocks = activeHardLocks,
            StuckInPickingOver2h = stuck
        });
    }

    private async Task<AgnumHealthDto> BuildAgnumHealthAsync(CancellationToken cancellationToken)
    {
        var lastExportQuery = _dbContext.AgnumExportHistories
            .AsNoTracking()
            .OrderByDescending(x => x.ExportedAt);
        var lastExport = await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(lastExportQuery, cancellationToken);

        var lastImportQuery = _dbContext.AgnumBalanceImportRuns
            .AsNoTracking()
            .OrderByDescending(x => x.StartedAt);
        var lastImport = await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(lastImportQuery, cancellationToken);

        var (matched, over, under, notLinked) = await ComputeReconciliationCountsAsync(cancellationToken);

        var exportFailed = lastExport is not null && lastExport.Status == AgnumExportStatus.Failed;
        var importFailed = lastImport is not null && string.Equals(lastImport.Status, "Failed", StringComparison.OrdinalIgnoreCase);
        var hasMismatch = over + under + notLinked > 0;
        var hasImportErrors = lastImport is not null && lastImport.ErrorCount > 0;
        var hasExportRetries = lastExport is not null && lastExport.RetryCount > 0;

        string overallStatus;
        if (lastExport is null && lastImport is null)
        {
            overallStatus = "Unknown";
        }
        else if (exportFailed || importFailed)
        {
            overallStatus = "Failed";
        }
        else if (hasMismatch || hasImportErrors || hasExportRetries)
        {
            overallStatus = "Degraded";
        }
        else
        {
            overallStatus = "Ok";
        }

        return new AgnumHealthDto
        {
            OverallStatus = overallStatus,
            ExportedAt = lastExport?.ExportedAt,
            ExportStatus = lastExport?.Status.ToString() ?? "Unknown",
            ExportRowCount = lastExport?.RowCount ?? 0,
            ExportRetryCount = lastExport?.RetryCount ?? 0,
            ExportError = lastExport?.ErrorMessage,
            ImportStartedAt = lastImport?.StartedAt,
            ImportFinishedAt = lastImport?.FinishedAt,
            ImportStatus = lastImport?.Status ?? "Unknown",
            ImportProductCount = lastImport?.ProductCount ?? 0,
            ImportBalanceCount = lastImport?.BalanceCount ?? 0,
            ImportErrorCount = lastImport?.ErrorCount ?? 0,
            ImportErrorSummary = lastImport?.ErrorSummary,
            ReconciliationMatched = matched,
            ReconciliationOver = over,
            ReconciliationUnder = under,
            ReconciliationNotLinked = notLinked
        };
    }

    private async Task<(int Matched, int Over, int Under, int NotLinked)> ComputeReconciliationCountsAsync(CancellationToken cancellationToken)
    {
        var sndIdsQuery = _dbContext.AgnumWarehouseMappings
            .AsNoTracking()
            .Where(x => x.IsImportEnabled)
            .Select(x => x.SndId);
        var sndIds = await EntityFrameworkQueryableExtensions.ToListAsync(sndIdsQuery, cancellationToken);

        if (sndIds.Count == 0)
        {
            return (0, 0, 0, 0);
        }

        var matched = 0;
        var over = 0;
        var under = 0;
        var notLinked = 0;

        foreach (var sndId in sndIds)
        {
            var latestRunQuery = _dbContext.AgnumBalanceImportRuns
                .AsNoTracking()
                .Where(x => x.SndId == sndId && x.Status == "Completed")
                .OrderByDescending(x => x.FinishedAt);
            var latestRun = await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(latestRunQuery, cancellationToken);

            if (latestRun is null)
            {
                continue;
            }

            var balanceRowsQuery = _dbContext.AgnumVirtualWarehouseBalances
                .AsNoTracking()
                .Where(x => x.ImportRunId == latestRun.Id)
                .Select(x => new { x.Id, x.Sku, x.Quantity });
            var balanceRows = await EntityFrameworkQueryableExtensions.ToListAsync(balanceRowsQuery, cancellationToken);

            if (balanceRows.Count == 0)
            {
                continue;
            }

            var balanceIds = balanceRows.Select(x => x.Id).ToList();
            var distributedByBalance = await _dbContext.AgnumBalanceDistributions
                .AsNoTracking()
                .Where(x => balanceIds.Contains(x.VirtualBalanceId))
                .GroupBy(x => x.VirtualBalanceId)
                .Select(g => new { VirtualBalanceId = g.Key, Total = g.Sum(x => x.Quantity) })
                .ToDictionaryAsync(x => x.VirtualBalanceId, x => x.Total, cancellationToken);

            var skus = balanceRows
                .Select(x => x.Sku)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var physicalBySku = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            if (skus.Count > 0)
            {
                await using var querySession = _documentStore.QuerySession();
                var physicalRows = await Marten.QueryableExtensions.ToListAsync(
                    querySession.Query<AvailableStockView>().Where(x => skus.Contains(x.SKU) && x.OnHandQty > 0m),
                    cancellationToken);

                physicalBySku = physicalRows
                    .GroupBy(x => x.SKU, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.Sum(y => y.OnHandQty), StringComparer.OrdinalIgnoreCase);
            }

            foreach (var row in balanceRows)
            {
                var distributedQty = distributedByBalance.GetValueOrDefault(row.Id);
                var physicalQty = string.IsNullOrWhiteSpace(row.Sku)
                    ? 0m
                    : physicalBySku.GetValueOrDefault(row.Sku);
                var delta = physicalQty - distributedQty;
                var status = AgnumReconciliationStatusCalculator.GetStatus(row.Sku, delta);

                switch (status)
                {
                    case "Matched": matched++; break;
                    case "Over": over++; break;
                    case "Under": under++; break;
                    default: notLinked++; break;
                }
            }
        }

        return (matched, over, under, notLinked);
    }

    private static string BucketFor(int daysRemaining) => daysRemaining switch
    {
        < 0 => "Expired",
        <= 7 => "0-7",
        <= 30 => "8-30",
        _ => "31-90"
    };
}

public sealed record HealthStatusDto
{
    public bool Ok { get; init; }
    public string Service { get; init; } = string.Empty;
    public string Version { get; init; } = "dev";
    public DateTime UtcNow { get; init; }
    public string Status { get; init; } = "Healthy";
    public double? ProjectionLag { get; init; }
    public DateTime? LastCheck { get; init; }
}

public sealed record ProjectionHealthDto
{
    public string ProjectionName { get; init; } = string.Empty;
    public long? HighWaterMark { get; init; }
    public long? LastProcessed { get; init; }
    public double? LagSeconds { get; init; }
    public long? LagEvents { get; init; }
    public DateTimeOffset? LastUpdated { get; init; }
    public string Status { get; init; } = "Unknown";
}

public sealed record StockSummaryDto
{
    public int TotalSKUs { get; init; }
    public decimal TotalQuantity { get; init; }
    public decimal TotalValue { get; init; }
}

public sealed record ReservationSummaryDto
{
    public int Allocated { get; init; }
    public int Picking { get; init; }
    public int Consumed { get; init; }
}

public sealed record RecentMovementDto
{
    public Guid MovementId { get; init; }
    public string SKU { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string FromLocation { get; init; } = string.Empty;
    public string ToLocation { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
}

public sealed record WarehouseOptionDto(string Code, string Name);

public sealed record DashboardOverviewDto
{
    public decimal TotalStockValue { get; init; }
    public int TotalSKUs { get; init; }
    public decimal TotalQuantity { get; init; }
    public int LowStockCount { get; init; }
    public int OutOfStockCount { get; init; }
    public int ExpiringSoonCount { get; init; }
    public int ExpiredCount { get; init; }
    public string AgnumStatus { get; init; } = "Unknown";
}

public sealed record CategoryValueDto(string CategoryName, decimal Value, decimal Percent);

public sealed record WarehouseValueDto(string WarehouseCode, string WarehouseName, decimal Value, decimal Quantity);

public sealed record LowStockResponseDto(IReadOnlyList<LowStockItemDto> Items, int TotalCount);

public sealed record LowStockItemDto(
    string SKU,
    string ItemName,
    string CategoryName,
    string BaseUoM,
    decimal OnHandQty,
    decimal AvailableQty,
    int ReorderPoint,
    string? SupplierName,
    int? LeadTimeDays,
    string Status);

public sealed record ExpiringLotDto(
    string SKU,
    string ItemName,
    string? LotNumber,
    string? LocationCode,
    decimal Qty,
    string BaseUoM,
    DateOnly ExpiryDate,
    int DaysRemaining,
    string Bucket);

public sealed record IncomingShipmentDto(
    string ReferenceNumber,
    string SupplierName,
    int TotalLines,
    DateOnly? ExpectedDate,
    string Status,
    decimal TotalReceivedQty,
    decimal TotalExpectedQty,
    decimal CompletionPercent,
    bool IsOverdue);

public sealed record AgnumHealthDto
{
    public string OverallStatus { get; init; } = "Unknown";
    public DateTimeOffset? ExportedAt { get; init; }
    public string ExportStatus { get; init; } = "Unknown";
    public int ExportRowCount { get; init; }
    public int ExportRetryCount { get; init; }
    public string? ExportError { get; init; }
    public DateTime? ImportStartedAt { get; init; }
    public DateTime? ImportFinishedAt { get; init; }
    public string ImportStatus { get; init; } = "Unknown";
    public int ImportProductCount { get; init; }
    public int ImportBalanceCount { get; init; }
    public int ImportErrorCount { get; init; }
    public string? ImportErrorSummary { get; init; }
    public int ReconciliationMatched { get; init; }
    public int ReconciliationOver { get; init; }
    public int ReconciliationUnder { get; init; }
    public int ReconciliationNotLinked { get; init; }
}

public sealed record ReservationFunnelDto
{
    public int Allocated { get; init; }
    public int Picking { get; init; }
    public int Consumed { get; init; }
    public int ActiveHardLocks { get; init; }
    public int StuckInPickingOver2h { get; init; }
}
