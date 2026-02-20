using System.Globalization;
using System.Text;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Infrastructure.Caching;
using LKvitai.MES.Infrastructure.Persistence;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
[Route("api/warehouse/v1/stock")]
public sealed class StockController : ControllerBase
{
    private const string DefaultWarehouseId = "WH1";

    private readonly IDocumentStore _documentStore;
    private readonly WarehouseDbContext _dbContext;

    public StockController(IDocumentStore documentStore, WarehouseDbContext dbContext)
    {
        _documentStore = documentStore;
        _dbContext = dbContext;
    }

    [HttpGet("available")]
    public async Task<IActionResult> GetAvailableAsync(
        [FromQuery] string? warehouse = null,
        [FromQuery] string? location = null,
        [FromQuery] string? sku = null,
        [FromQuery] int? itemId = null,
        [FromQuery] int? locationId = null,
        [FromQuery] int? categoryId = null,
        [FromQuery] bool includeReserved = false,
        [FromQuery] bool? includeVirtual = null,
        [FromQuery] bool includeVirtualLocations = false,
        [FromQuery] DateOnly? expiringBefore = null,
        [FromQuery] int? page = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool exportCsv = false,
        CancellationToken cancellationToken = default)
    {
        var canUseCache = itemId.HasValue &&
                          locationId.HasValue &&
                          !exportCsv &&
                          string.IsNullOrWhiteSpace(location) &&
                          string.IsNullOrWhiteSpace(sku) &&
                          !categoryId.HasValue &&
                          !expiringBefore.HasValue &&
                          pageNumber == 1 &&
                          pageSize == 50;
        var cacheKey = canUseCache
            ? $"stock:{itemId!.Value}:{locationId!.Value}"
            : string.Empty;
        if (canUseCache)
        {
            var cached = await Cache.GetAsync<PagedStockResponse<AvailableStockItemDto>>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                return Ok(cached);
            }
        }

        pageNumber = page ?? pageNumber;
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 1000);
        includeVirtualLocations = includeVirtual ?? includeVirtualLocations;

        await using var querySession = _documentStore.QuerySession();
        IQueryable<AvailableStockView> query = querySession
            .Query<AvailableStockView>()
            .Where(x => x.OnHandQty != 0m);

        if (string.IsNullOrWhiteSpace(warehouse))
        {
            query = query.Where(x => x.WarehouseId == DefaultWarehouseId);
        }
        else
        {
            var normalizedWarehouse = warehouse.Trim();
            query = query.Where(x => x.WarehouseId == normalizedWarehouse);
        }

        if (expiringBefore.HasValue)
        {
            query = query.Where(x => x.ExpiryDate != null && x.ExpiryDate <= expiringBefore.Value);
        }

        var rows = await Marten.QueryableExtensions.ToListAsync(query, cancellationToken);

        var skus = rows.Select(x => x.SKU).Distinct().ToList();
        var locationCodes = rows.Select(x => x.LocationCode ?? x.Location).Distinct().ToList();

        var itemMap = await _dbContext.Items
            .AsNoTracking()
            .Where(x => skus.Contains(x.InternalSKU))
            .ToDictionaryAsync(x => x.InternalSKU, cancellationToken);

        var locationMap = await LoadLocationsByCodeAsync(locationCodes, cancellationToken);

        var mapped = rows.Select(row =>
        {
            itemMap.TryGetValue(row.SKU, out var item);
            var code = row.LocationCode ?? row.Location;
            locationMap.TryGetValue(code, out var location);

            return new AvailableStockRow(
                item?.Id ?? row.ItemId,
                row.SKU,
                item?.Name ?? row.ItemName ?? row.SKU,
                location?.Id,
                code,
                row.LotNumber,
                row.ExpiryDate,
                row.OnHandQty,
                includeReserved ? row.ReservedQty : 0m,
                row.AvailableQty,
                row.BaseUoM ?? item?.BaseUoM ?? string.Empty,
                row.LastUpdated,
                item?.CategoryId,
                location?.IsVirtual ?? false);
        });

        if (itemId.HasValue)
        {
            mapped = mapped.Where(x => x.ItemId == itemId.Value);
        }

        if (locationId.HasValue)
        {
            mapped = mapped.Where(x => x.LocationId == locationId.Value);
        }

        if (categoryId.HasValue)
        {
            mapped = mapped.Where(x => x.CategoryId == categoryId.Value);
        }

        if (!includeVirtualLocations)
        {
            mapped = mapped.Where(x => !x.IsVirtualLocation);
        }

        var filtered = mapped
            .OrderBy(x => x.InternalSku)
            .ThenBy(x => x.LocationCode)
            .ThenBy(x => x.LotNumber)
            .ToList();

        if (!string.IsNullOrWhiteSpace(location))
        {
            var normalizedLocation = location.Trim();
            filtered = filtered
                .Where(x => MatchesPattern(x.LocationCode, normalizedLocation))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(sku))
        {
            var normalizedSku = sku.Trim();
            filtered = filtered
                .Where(x => MatchesPattern(x.InternalSku, normalizedSku))
                .ToList();
        }

        if (exportCsv)
        {
            return File(
                Encoding.UTF8.GetBytes(BuildAvailableCsv(filtered)),
                "text/csv",
                "stock-available.csv");
        }

        var totalCount = filtered.Count;
        var items = filtered
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var projectionTimestamp = filtered.Count > 0
            ? filtered.Max(x => x.LastUpdated)
            : DateTime.UtcNow;

        var response = new PagedStockResponse<AvailableStockItemDto>(
            items.Select(x => new AvailableStockItemDto(
                x.ItemId,
                x.InternalSku,
                x.ItemName,
                x.LocationId,
                x.LocationCode,
                x.LotNumber,
                x.ExpiryDate,
                x.Qty,
                x.ReservedQty,
                x.AvailableQty,
                x.BaseUom,
                x.LastUpdated)).ToList(),
            totalCount,
            pageNumber,
            pageSize,
            projectionTimestamp);

        if (canUseCache)
        {
            await Cache.SetAsync(cacheKey, response, TimeSpan.FromSeconds(30), cancellationToken);
        }

        return Ok(response);
    }

    [HttpGet("location-balance")]
    public async Task<IActionResult> GetLocationBalanceAsync(
        [FromQuery] int? locationId = null,
        [FromQuery] string? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool exportCsv = false,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 1000);

        await using var querySession = _documentStore.QuerySession();
        var rows = await Marten.QueryableExtensions.ToListAsync(
            querySession.Query<LocationBalanceView>()
                .Where(x => x.WarehouseId == DefaultWarehouseId),
            cancellationToken);

        var locationCodes = rows.Select(x => x.Location).Distinct().ToList();
        var skus = rows.Select(x => x.SKU).Distinct().ToList();

        var locations = await LoadLocationsByCodeAsync(locationCodes, cancellationToken);

        var items = await _dbContext.Items
            .AsNoTracking()
            .Where(x => skus.Contains(x.InternalSKU))
            .ToDictionaryAsync(x => x.InternalSKU, cancellationToken);

        var grouped = rows
            .GroupBy(x => x.Location)
            .Select(group =>
            {
                locations.TryGetValue(group.Key, out var location);

                decimal totalWeight = 0m;
                decimal totalVolume = 0m;
                foreach (var row in group)
                {
                    if (!items.TryGetValue(row.SKU, out var item))
                    {
                        continue;
                    }

                    totalWeight += (item.Weight ?? 0m) * row.Quantity;
                    totalVolume += (item.Volume ?? 0m) * row.Quantity;
                }

                var utilizationWeight = location?.MaxWeight > 0m
                    ? totalWeight / location.MaxWeight.Value
                    : (decimal?)null;
                var utilizationVolume = location?.MaxVolume > 0m
                    ? totalVolume / location.MaxVolume.Value
                    : (decimal?)null;

                return new LocationBalanceItemDto(
                    location?.Id,
                    group.Key,
                    group.Select(x => x.SKU).Distinct().Count(),
                    totalWeight,
                    totalVolume,
                    location?.MaxWeight,
                    location?.MaxVolume,
                    utilizationWeight,
                    utilizationVolume,
                    location?.Status ?? "Unknown");
            })
            .ToList();

        if (locationId.HasValue)
        {
            grouped = grouped.Where(x => x.LocationId == locationId.Value).ToList();
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim();
            grouped = grouped.Where(x => x.Status == normalized).ToList();
        }

        grouped = grouped.OrderBy(x => x.LocationCode).ToList();

        if (exportCsv)
        {
            return File(
                Encoding.UTF8.GetBytes(BuildLocationBalanceCsv(grouped)),
                "text/csv",
                "location-balance.csv");
        }

        var totalCount = grouped.Count;
        var itemsPage = grouped
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new PagedResponse<LocationBalanceItemDto>(itemsPage, totalCount, pageNumber, pageSize));
    }

    private static string BuildAvailableCsv(IReadOnlyCollection<AvailableStockRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ItemId,InternalSKU,ItemName,LocationId,LocationCode,LotNumber,ExpiryDate,Qty,ReservedQty,AvailableQty,BaseUoM,LastUpdated");
        foreach (var row in rows)
        {
            sb.Append(row.ItemId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',');
            sb.Append(EscapeCsv(row.InternalSku)).Append(',');
            sb.Append(EscapeCsv(row.ItemName)).Append(',');
            sb.Append(row.LocationId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',');
            sb.Append(EscapeCsv(row.LocationCode)).Append(',');
            sb.Append(EscapeCsv(row.LotNumber)).Append(',');
            sb.Append(row.ExpiryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty).Append(',');
            sb.Append(row.Qty.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(row.ReservedQty.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(row.AvailableQty.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv(row.BaseUom)).Append(',');
            sb.Append(row.LastUpdated.ToString("O", CultureInfo.InvariantCulture));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string BuildLocationBalanceCsv(IReadOnlyCollection<LocationBalanceItemDto> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("LocationId,LocationCode,ItemCount,TotalWeight,TotalVolume,MaxWeight,MaxVolume,UtilizationWeight,UtilizationVolume,Status");
        foreach (var row in rows)
        {
            sb.Append(row.LocationId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',');
            sb.Append(EscapeCsv(row.LocationCode)).Append(',');
            sb.Append(row.ItemCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(row.TotalWeight.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(row.TotalVolume.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(row.MaxWeight?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',');
            sb.Append(row.MaxVolume?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',');
            sb.Append(row.UtilizationWeight?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',');
            sb.Append(row.UtilizationVolume?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',');
            sb.Append(EscapeCsv(row.Status));
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

    private static bool MatchesPattern(string value, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return true;
        }

        var normalizedPattern = pattern.Trim();
        if (normalizedPattern == "*")
        {
            return true;
        }

        var isPrefix = normalizedPattern.EndsWith('*');
        var isSuffix = normalizedPattern.StartsWith('*');
        var token = normalizedPattern.Trim('*');
        if (string.IsNullOrEmpty(token))
        {
            return true;
        }

        var comparison = StringComparison.OrdinalIgnoreCase;
        if (isPrefix && isSuffix)
        {
            return value.Contains(token, comparison);
        }

        if (isPrefix)
        {
            return value.StartsWith(token, comparison);
        }

        if (isSuffix)
        {
            return value.EndsWith(token, comparison);
        }

        return value.Contains(token, comparison);
    }

    private async Task<Dictionary<string, Location>> LoadLocationsByCodeAsync(
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, Location>(StringComparer.OrdinalIgnoreCase);
        var misses = new List<string>();

        foreach (var code in codes)
        {
            var cached = await Cache.GetAsync<Location>($"location:{code}", cancellationToken);
            if (cached is not Location cachedLocation)
            {
                misses.Add(code);
            }
            else
            {
                result[code] = cachedLocation;
            }
        }

        if (misses.Count == 0)
        {
            return result;
        }

        var loaded = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            _dbContext.Locations
                .AsNoTracking()
                .Where(x => misses.Contains(x.Code)),
            cancellationToken);

        foreach (var row in loaded)
        {
            result[row.Code] = row;
            await Cache.SetAsync($"location:{row.Code}", row, TimeSpan.FromHours(2), cancellationToken);
        }

        return result;
    }

    private ICacheService Cache => HttpContext?.RequestServices?.GetService<ICacheService>() ?? new LKvitai.MES.Infrastructure.Caching.NoOpCacheService();

    private sealed record AvailableStockRow(
        int? ItemId,
        string InternalSku,
        string ItemName,
        int? LocationId,
        string LocationCode,
        string? LotNumber,
        DateOnly? ExpiryDate,
        decimal Qty,
        decimal ReservedQty,
        decimal AvailableQty,
        string BaseUom,
        DateTime LastUpdated,
        int? CategoryId,
        bool IsVirtualLocation);

    public sealed record AvailableStockItemDto(
        int? ItemId,
        string InternalSku,
        string ItemName,
        int? LocationId,
        string LocationCode,
        string? LotNumber,
        DateOnly? ExpiryDate,
        decimal Qty,
        decimal ReservedQty,
        decimal AvailableQty,
        string BaseUom,
        DateTime LastUpdated);

    public sealed record PagedStockResponse<T>(
        IReadOnlyList<T> Items,
        int TotalCount,
        int PageNumber,
        int PageSize,
        DateTime ProjectionTimestamp);

    public sealed record LocationBalanceItemDto(
        int? LocationId,
        string LocationCode,
        int ItemCount,
        decimal TotalWeight,
        decimal TotalVolume,
        decimal? MaxWeight,
        decimal? MaxVolume,
        decimal? UtilizationWeight,
        decimal? UtilizationVolume,
        string Status);

    public sealed record PagedResponse<T>(
        IReadOnlyList<T> Items,
        int TotalCount,
        int PageNumber,
        int PageSize);
}
