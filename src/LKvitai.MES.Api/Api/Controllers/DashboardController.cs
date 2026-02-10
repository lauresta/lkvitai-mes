using System.Reflection;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Infrastructure.Persistence;
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

    public DashboardController(IDocumentStore documentStore, WarehouseDbContext dbContext)
    {
        _documentStore = documentStore;
        _dbContext = dbContext;
    }

    [HttpGet("health")]
    public ActionResult<HealthStatusDto> GetHealth()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "dev";

        var response = new HealthStatusDto
        {
            Ok = true,
            Service = "LKvitai.MES.Api",
            Version = version,
            UtcNow = DateTime.UtcNow
        };

        return Ok(response);
    }

    [HttpGet("projection-health")]
    public async Task<ActionResult<IReadOnlyList<ProjectionHealthDto>>> GetProjectionHealthAsync(CancellationToken cancellationToken)
    {
        var allProgress = await _documentStore.Advanced.AllProjectionProgress(token: cancellationToken);
        var eventStoreStatistics = await _documentStore.Advanced.FetchEventStoreStatistics(token: cancellationToken);
        var highWaterMark = (long?)eventStoreStatistics.EventSequenceNumber;

        var projections = allProgress
            .Select(progress => new
            {
                ProjectionName = ResolveProjectionName(progress.ShardName),
                LastProcessed = (long?)progress.Sequence
            })
            .Where(progress => !string.IsNullOrWhiteSpace(progress.ProjectionName))
            .GroupBy(progress => progress.ProjectionName, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var lastProcessed = group.Max(progress => progress.LastProcessed);
                return new ProjectionHealthDto
                {
                    ProjectionName = group.Key,
                    HighWaterMark = highWaterMark,
                    LastProcessed = lastProcessed,
                    LagSeconds = null
                };
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

        await using var querySession = _documentStore.QuerySession();
        var rows = await Marten.QueryableExtensions.ToListAsync(
            querySession.Query<AdjustmentHistoryView>()
                .OrderByDescending(x => x.Timestamp)
                .Take(normalizedLimit),
            cancellationToken);

        var items = rows.Select(x =>
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
        }).ToList();

        return Ok(items);
    }

    private static string ResolveProjectionName(string? shardName)
    {
        if (string.IsNullOrWhiteSpace(shardName))
        {
            return string.Empty;
        }

        var separatorIndex = shardName.IndexOf(':');
        if (separatorIndex <= 0)
        {
            return shardName;
        }

        return shardName[..separatorIndex];
    }
}

public sealed record HealthStatusDto
{
    public bool Ok { get; init; }
    public string Service { get; init; } = string.Empty;
    public string Version { get; init; } = "dev";
    public DateTime UtcNow { get; init; }
}

public sealed record ProjectionHealthDto
{
    public string ProjectionName { get; init; } = string.Empty;
    public long? HighWaterMark { get; init; }
    public long? LastProcessed { get; init; }
    public double? LagSeconds { get; init; }
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
