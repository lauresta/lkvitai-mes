using System.Reflection;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private const string DefaultWarehouseId = "WH1";

    private readonly IDocumentStore _documentStore;
    private readonly WarehouseDbContext _dbContext;
    private readonly IProjectionHealthService _projectionHealthService;

    public DashboardController(
        IDocumentStore documentStore,
        WarehouseDbContext dbContext,
        IProjectionHealthService projectionHealthService)
    {
        _documentStore = documentStore;
        _dbContext = dbContext;
        _projectionHealthService = projectionHealthService;
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
            Service = "LKvitai.MES.Api",
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
