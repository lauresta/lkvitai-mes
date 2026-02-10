using System.Diagnostics;
using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Application.Services;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Domain;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using IDocumentStore = Marten.IDocumentStore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
[Route("api/warehouse/v1/putaway")]
public sealed class PutawayController : ControllerBase
{
    private const string DefaultWarehouseId = "WH1";

    private readonly WarehouseDbContext _dbContext;
    private readonly IDocumentStore _documentStore;
    private readonly ICurrentUserService _currentUserService;

    public PutawayController(
        WarehouseDbContext dbContext,
        IDocumentStore documentStore,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _documentStore = documentStore;
        _currentUserService = currentUserService;
    }

    [HttpGet("tasks")]
    public async Task<IActionResult> GetTasksAsync(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 500);

        await using var querySession = _documentStore.QuerySession();
        IQueryable<AvailableStockView> query = querySession.Query<AvailableStockView>()
            .Where(x => x.WarehouseId == DefaultWarehouseId && x.Location == "RECEIVING" && x.AvailableQty > 0m);

        var totalCount = await Marten.QueryableExtensions.CountAsync(query, cancellationToken);
        var rows = await Marten.QueryableExtensions.ToListAsync(
            query.OrderBy(x => x.LastUpdated)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize),
            cancellationToken);

        var itemBySku = await _dbContext.Items
            .AsNoTracking()
            .ToDictionaryAsync(x => x.InternalSKU, cancellationToken);

        var result = rows.Select(x =>
        {
            var item = itemBySku.GetValueOrDefault(x.SKU);
            return new PutawayTaskDto(
                item?.Id ?? x.ItemId ?? 0,
                x.SKU,
                item?.Name ?? x.ItemName ?? x.SKU,
                x.AvailableQty,
                x.LotNumber,
                x.LastUpdated,
                x.Location);
        }).ToList();

        return Ok(new PagedResponse<PutawayTaskDto>(result, totalCount, pageNumber, pageSize));
    }

    [HttpPost]
    [Authorize(Policy = WarehousePolicies.QcOrManager)]
    public async Task<IActionResult> PutawayAsync(
        [FromBody] PutawayRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ItemId <= 0)
        {
            return ValidationFailure("Field 'itemId' is required.");
        }

        if (request.Qty <= 0m)
        {
            return ValidationFailure("Field 'qty' must be greater than 0.");
        }

        var item = await _dbContext.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ItemId, cancellationToken);
        if (item is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Item '{request.ItemId}' does not exist."));
        }

        var fromLocation = await _dbContext.Locations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.FromLocationId, cancellationToken);
        var toLocation = await _dbContext.Locations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ToLocationId, cancellationToken);

        if (fromLocation is null || toLocation is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, "Source or destination location does not exist."));
        }

        if (!string.Equals(fromLocation.Code, "RECEIVING", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationFailure("Putaway source location must be RECEIVING.");
        }

        await using var querySession = _documentStore.QuerySession();
        var availableQty = await Marten.QueryableExtensions.SumAsync(
            querySession.Query<AvailableStockView>()
                .Where(x => x.WarehouseId == DefaultWarehouseId &&
                            x.Location == fromLocation.Code &&
                            x.SKU == item.InternalSKU),
            x => x.AvailableQty,
            cancellationToken);

        if (availableQty < request.Qty)
        {
            return UnprocessableFailure(
                $"Item {item.InternalSKU} at location {fromLocation.Code} has only {availableQty} available. Cannot move {request.Qty}.");
        }

        var warning = await BuildCapacityWarningAsync(toLocation, item, request.Qty, cancellationToken);

        var now = DateTime.UtcNow;
        var evt = new StockMovedEvent
        {
            MovementId = Guid.NewGuid(),
            SKU = item.InternalSKU,
            Quantity = request.Qty,
            FromLocation = fromLocation.Code,
            ToLocation = toLocation.Code,
            MovementType = "Putaway",
            OperatorId = Guid.Empty,
            Reason = request.Notes,
            Timestamp = now
        };

        await using (var session = _documentStore.LightweightSession())
        {
            var streamId = StockLedgerStreamId.For(DefaultWarehouseId, fromLocation.Code, item.InternalSKU);
            session.Events.Append(streamId, evt);
            await session.SaveChangesAsync(cancellationToken);
        }

        return Ok(new PutawayResponse(
            evt.EventId,
            item.Id,
            request.Qty,
            fromLocation.Id,
            fromLocation.Code,
            toLocation.Id,
            toLocation.Code,
            now,
            warning));
    }

    private async Task<string?> BuildCapacityWarningAsync(
        Domain.Entities.Location toLocation,
        Domain.Entities.Item item,
        decimal qty,
        CancellationToken cancellationToken)
    {
        if (!toLocation.MaxWeight.HasValue && !toLocation.MaxVolume.HasValue)
        {
            return null;
        }

        await using var querySession = _documentStore.QuerySession();
        var locationRows = await Marten.QueryableExtensions.ToListAsync(
            querySession.Query<AvailableStockView>()
                .Where(x => x.WarehouseId == DefaultWarehouseId && x.Location == toLocation.Code),
            cancellationToken);

        if (locationRows.Count == 0)
        {
            var itemWeight = item.Weight ?? 0m;
            var itemVolume = item.Volume ?? 0m;

            if (toLocation.MaxWeight.HasValue && itemWeight > 0m)
            {
                var weightUtilization = (itemWeight * qty) / toLocation.MaxWeight.Value;
                if (weightUtilization > 0.8m)
                {
                    return $"Capacity warning: weight utilization would be {weightUtilization:P0}.";
                }
            }

            if (toLocation.MaxVolume.HasValue && itemVolume > 0m)
            {
                var volumeUtilization = (itemVolume * qty) / toLocation.MaxVolume.Value;
                if (volumeUtilization > 0.8m)
                {
                    return $"Capacity warning: volume utilization would be {volumeUtilization:P0}.";
                }
            }

            return null;
        }

        var skus = locationRows.Select(x => x.SKU).Distinct().ToList();
        var itemsBySku = await _dbContext.Items
            .AsNoTracking()
            .Where(x => skus.Contains(x.InternalSKU))
            .ToDictionaryAsync(x => x.InternalSKU, cancellationToken);

        decimal totalWeight = 0m;
        decimal totalVolume = 0m;

        foreach (var row in locationRows)
        {
            if (!itemsBySku.TryGetValue(row.SKU, out var rowItem))
            {
                continue;
            }

            totalWeight += (rowItem.Weight ?? 0m) * row.OnHandQty;
            totalVolume += (rowItem.Volume ?? 0m) * row.OnHandQty;
        }

        totalWeight += (item.Weight ?? 0m) * qty;
        totalVolume += (item.Volume ?? 0m) * qty;

        if (toLocation.MaxWeight.HasValue && toLocation.MaxWeight.Value > 0m)
        {
            var weightUtilization = totalWeight / toLocation.MaxWeight.Value;
            if (weightUtilization > 0.8m)
            {
                return $"Capacity warning: weight utilization would be {weightUtilization:P0}.";
            }
        }

        if (toLocation.MaxVolume.HasValue && toLocation.MaxVolume.Value > 0m)
        {
            var volumeUtilization = totalVolume / toLocation.MaxVolume.Value;
            if (volumeUtilization > 0.8m)
            {
                return $"Capacity warning: volume utilization would be {volumeUtilization:P0}.";
            }
        }

        return null;
    }

    private ObjectResult ValidationFailure(string detail)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(
            DomainErrorCodes.ValidationError,
            detail,
            HttpContext);

        return new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status400BadRequest
        };
    }

    private ObjectResult UnprocessableFailure(string detail)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(
            DomainErrorCodes.ValidationError,
            detail,
            HttpContext);

        return new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status422UnprocessableEntity
        };
    }

    private ObjectResult Failure(Result result)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(result, HttpContext);
        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };
    }

    public sealed record PutawayTaskDto(
        int ItemId,
        string InternalSKU,
        string ItemName,
        decimal Qty,
        string? LotNumber,
        DateTime ReceivedAt,
        string FromLocationCode);

    public sealed record PutawayRequest(
        int ItemId,
        decimal Qty,
        int FromLocationId,
        int ToLocationId,
        int? LotId,
        string? Notes);

    public sealed record PutawayResponse(
        Guid EventId,
        int ItemId,
        decimal Qty,
        int FromLocationId,
        string FromLocationCode,
        int ToLocationId,
        string ToLocationCode,
        DateTime Timestamp,
        string? Warning);

    public sealed record PagedResponse<T>(
        IReadOnlyList<T> Items,
        int TotalCount,
        int PageNumber,
        int PageSize);
}
