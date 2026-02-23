using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/lots")]
public sealed class LotsController : ControllerBase
{
    private readonly WarehouseDbContext _dbContext;
    private readonly IDocumentStore _documentStore;

    public LotsController(WarehouseDbContext dbContext, IDocumentStore documentStore)
    {
        _dbContext = dbContext;
        _documentStore = documentStore;
    }

    [HttpGet]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetAsync(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var lotQuery = from lot in _dbContext.Lots.AsNoTracking()
                       join item in _dbContext.Items.AsNoTracking() on lot.ItemId equals item.Id into itemGroup
                       from item in itemGroup.DefaultIfEmpty()
                       select new LotProjection(
                           lot.Id,
                           lot.ItemId,
                           lot.LotNumber,
                           lot.ProductionDate,
                           lot.ExpiryDate,
                           item != null ? item.InternalSKU : string.Empty,
                           item != null ? item.Name : string.Empty,
                           item != null ? item.BaseUoM : string.Empty);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToLowerInvariant();
            lotQuery = lotQuery.Where(x =>
                x.LotNumber.ToLower().Contains(normalized) ||
                x.ItemSku.ToLower().Contains(normalized) ||
                x.ItemName.ToLower().Contains(normalized));
        }

        var lots = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
            lotQuery.OrderBy(x => x.LotNumber),
            cancellationToken);

        var lotNumbers = lots
            .Select(x => x.LotNumber)
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Dictionary<string, LotQuantityProjection> quantityMap;
        if (lotNumbers.Count == 0)
        {
            quantityMap = new Dictionary<string, LotQuantityProjection>(StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            await using var querySession = _documentStore.QuerySession();
            var stockRows = await Marten.QueryableExtensions.ToListAsync(
                querySession.Query<AvailableStockView>()
                    .Where(x => x.LotNumber != null && lotNumbers.Contains(x.LotNumber)),
                cancellationToken);

            quantityMap = stockRows
                .Where(x => !string.IsNullOrWhiteSpace(x.LotNumber))
                .GroupBy(x => BuildLotKey(x.ItemId, x.LotNumber!))
                .ToDictionary(
                    x => x.Key,
                    x => new LotQuantityProjection(
                        x.Sum(v => v.OnHandQty),
                        x.Sum(v => v.ReservedQty),
                        x.Sum(v => v.AvailableQty),
                        x.Select(v => v.ExpiryDate).FirstOrDefault(v => v != null)),
                    StringComparer.OrdinalIgnoreCase);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var merged = lots
            .Select(x =>
            {
                quantityMap.TryGetValue(BuildLotKey(x.ItemId, x.LotNumber), out var quantity);

                var expiry = x.ExpiryDate ?? quantity?.ExpiryDate;
                var qty = quantity?.Qty ?? 0m;
                var reserved = quantity?.ReservedQty ?? 0m;
                var available = quantity?.AvailableQty ?? 0m;
                var rowStatus = ComputeStatus(today, expiry, available);

                return new LotListItemDto(
                    x.Id,
                    x.ItemId,
                    x.ItemSku,
                    x.ItemName,
                    x.LotNumber,
                    x.ProductionDate,
                    expiry,
                    qty,
                    reserved,
                    available,
                    x.BaseUom,
                    rowStatus);
            });

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim();
            merged = merged.Where(x => string.Equals(x.Status, normalizedStatus, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = merged.ToList();
        var totalCount = filtered.Count;
        var pageItems = filtered
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(new PagedResponse<LotListItemDto>(pageItems, totalCount, pageNumber, pageSize));
    }

    private static string BuildLotKey(int? itemId, string lotNumber) => $"{itemId ?? 0}:{lotNumber}".ToLowerInvariant();

    private static string ComputeStatus(DateOnly today, DateOnly? expiryDate, decimal availableQty)
    {
        if (availableQty <= 0m)
        {
            return "Depleted";
        }

        if (expiryDate.HasValue && expiryDate.Value < today)
        {
            return "Expired";
        }

        if (expiryDate.HasValue && expiryDate.Value <= today.AddDays(30))
        {
            return "ExpiringSoon";
        }

        return "Active";
    }

    private sealed record LotProjection(
        int Id,
        int ItemId,
        string LotNumber,
        DateOnly? ProductionDate,
        DateOnly? ExpiryDate,
        string ItemSku,
        string ItemName,
        string BaseUom);

    private sealed record LotQuantityProjection(
        decimal Qty,
        decimal ReservedQty,
        decimal AvailableQty,
        DateOnly? ExpiryDate);

    public sealed record LotListItemDto(
        int Id,
        int ItemId,
        string ItemSku,
        string ItemName,
        string LotNumber,
        DateOnly? ProductionDate,
        DateOnly? ExpiryDate,
        decimal Qty,
        decimal ReservedQty,
        decimal AvailableQty,
        string BaseUom,
        string Status);

    public sealed record PagedResponse<T>(
        IReadOnlyList<T> Items,
        int TotalCount,
        int PageNumber,
        int PageSize);
}
