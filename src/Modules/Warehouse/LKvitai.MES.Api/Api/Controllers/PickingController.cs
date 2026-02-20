using System.Diagnostics;
using System.Globalization;
using System.Text;
using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Application.Services;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using IDocumentStore = Marten.IDocumentStore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/picking")]
public sealed class PickingController : ControllerBase
{
    private const string DefaultWarehouseId = "WH1";

    private readonly WarehouseDbContext _dbContext;
    private readonly IDocumentStore _documentStore;
    private readonly ICurrentUserService _currentUserService;

    public PickingController(
        WarehouseDbContext dbContext,
        IDocumentStore documentStore,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _documentStore = documentStore;
        _currentUserService = currentUserService;
    }

    [HttpPost("tasks")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> CreateTaskAsync(
        [FromBody] CreatePickTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.OrderId))
        {
            return ValidationFailure("Field 'orderId' is required.");
        }

        if (request.ItemId <= 0)
        {
            return ValidationFailure("Field 'itemId' is required.");
        }

        if (request.Qty <= 0m)
        {
            return ValidationFailure("Field 'qty' must be greater than 0.");
        }

        var itemExists = await _dbContext.Items
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.ItemId, cancellationToken);
        if (!itemExists)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Item '{request.ItemId}' does not exist."));
        }

        var task = new PickTask
        {
            TaskId = Guid.NewGuid(),
            OrderId = request.OrderId.Trim(),
            ItemId = request.ItemId,
            Qty = request.Qty,
            Status = "Pending",
            AssignedToUserId = string.IsNullOrWhiteSpace(request.AssignedToUserId)
                ? null
                : request.AssignedToUserId.Trim()
        };

        _dbContext.PickTasks.Add(task);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/warehouse/v1/picking/tasks/{task.TaskId}",
            new PickTaskCreatedResponse(
                task.TaskId,
                task.OrderId,
                task.ItemId,
                task.Qty,
                task.Status,
                task.CreatedAt));
    }

    [HttpGet("tasks/{id:guid}/locations")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetLocationSuggestionsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var task = await _dbContext.PickTasks
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TaskId == id, cancellationToken);

        if (task is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Pick task '{id}' does not exist."));
        }

        var item = await _dbContext.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == task.ItemId, cancellationToken);
        if (item is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Item '{task.ItemId}' does not exist."));
        }

        await using var querySession = _documentStore.QuerySession();
        IQueryable<AvailableStockView> query = querySession.Query<AvailableStockView>()
            .Where(x => x.WarehouseId == DefaultWarehouseId && x.SKU == item.InternalSKU && x.AvailableQty > 0m)
            .OrderBy(x => x.ExpiryDate == null)
            .ThenBy(x => x.ExpiryDate)
            .ThenByDescending(x => x.AvailableQty)
            .Take(5);

        var rows = await Marten.QueryableExtensions.ToListAsync(query, cancellationToken);

        var response = rows.Select(x => new PickLocationSuggestion(
            x.Location,
            x.AvailableQty,
            x.ExpiryDate,
            x.LotNumber)).ToList();

        return Ok(new PickLocationSuggestionResponse(task.TaskId, item.Id, item.InternalSKU, response));
    }

    [HttpPost("tasks/{id:guid}/complete")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> CompleteTaskAsync(
        Guid id,
        [FromBody] CompletePickTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.FromLocationId <= 0)
        {
            return ValidationFailure("Field 'fromLocationId' is required.");
        }

        if (request.PickedQty <= 0m)
        {
            return ValidationFailure("Field 'pickedQty' must be greater than 0.");
        }

        var task = await _dbContext.PickTasks
            .FirstOrDefaultAsync(x => x.TaskId == id, cancellationToken);

        if (task is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Pick task '{id}' does not exist."));
        }

        if (!string.Equals(task.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationFailure($"Pick task '{id}' is already '{task.Status}'.");
        }

        var item = await _dbContext.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == task.ItemId, cancellationToken);
        if (item is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Item '{task.ItemId}' does not exist."));
        }

        var fromLocation = await _dbContext.Locations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.FromLocationId, cancellationToken);
        if (fromLocation is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"From location '{request.FromLocationId}' does not exist."));
        }

        if (!string.IsNullOrWhiteSpace(request.ScannedLocationBarcode) &&
            !string.Equals(fromLocation.Barcode, request.ScannedLocationBarcode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return UnprocessableFailure(
                $"Scanned location barcode '{request.ScannedLocationBarcode}' does not match expected location '{fromLocation.Code}'.");
        }

        if (!string.IsNullOrWhiteSpace(request.ScannedBarcode))
        {
            var normalizedBarcode = request.ScannedBarcode.Trim();
            var barcodeMatches = string.Equals(item.PrimaryBarcode, normalizedBarcode, StringComparison.OrdinalIgnoreCase) ||
                                 await _dbContext.ItemBarcodes
                                     .AsNoTracking()
                                     .AnyAsync(x => x.ItemId == item.Id && x.Barcode == normalizedBarcode, cancellationToken);

            if (!barcodeMatches)
            {
                return UnprocessableFailure(
                    $"Scanned barcode '{request.ScannedBarcode}' does not match expected item {item.InternalSKU}.");
            }
        }

        await using var querySession = _documentStore.QuerySession();
        var availableQty = await Marten.QueryableExtensions.SumAsync(
            querySession.Query<AvailableStockView>()
                .Where(x => x.WarehouseId == DefaultWarehouseId &&
                            x.Location == fromLocation.Code &&
                            x.SKU == item.InternalSKU),
            x => x.AvailableQty,
            cancellationToken);

        if (availableQty < request.PickedQty)
        {
            return UnprocessableFailure(
                $"Available quantity at location '{fromLocation.Code}' is {availableQty}. Cannot pick {request.PickedQty}.");
        }

        var toLocation = await _dbContext.Locations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == "SHIPPING", cancellationToken);
        if (toLocation is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.InternalError, "Virtual location 'SHIPPING' is missing."));
        }

        var now = DateTime.UtcNow;
        var evt = new PickCompletedEvent
        {
            AggregateId = task.TaskId,
            UserId = _currentUserService.GetCurrentUserId(),
            TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            WarehouseId = DefaultWarehouseId,
            PickTaskId = task.TaskId,
            OrderId = task.OrderId,
            ItemId = item.Id,
            SKU = item.InternalSKU,
            PickedQty = request.PickedQty,
            FromLocationId = fromLocation.Id,
            FromLocation = fromLocation.Code,
            ToLocationId = toLocation.Id,
            ToLocation = toLocation.Code,
            LotId = request.LotId,
            LotNumber = request.LotId.HasValue
                ? await _dbContext.Lots
                    .AsNoTracking()
                    .Where(x => x.Id == request.LotId.Value)
                    .Select(x => x.LotNumber)
                    .FirstOrDefaultAsync(cancellationToken)
                : null,
            ScannedBarcode = request.ScannedBarcode,
            Notes = request.Notes,
            Timestamp = now
        };

        await using (var session = _documentStore.LightweightSession())
        {
            session.Events.Append($"pick-task:{task.TaskId:N}", evt);
            await session.SaveChangesAsync(cancellationToken);
        }

        task.Status = "Completed";
        task.PickedQty = request.PickedQty;
        task.FromLocationId = fromLocation.Id;
        task.ToLocationId = toLocation.Id;
        task.LotId = request.LotId;
        task.CompletedAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new CompletePickTaskResponse(
            task.TaskId,
            evt.EventId,
            item.Id,
            request.PickedQty,
            fromLocation.Id,
            toLocation.Id,
            task.Status,
            now));
    }

    [HttpGet("history")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetHistoryAsync(
        [FromQuery] int? itemId = null,
        [FromQuery] string? orderId = null,
        [FromQuery] string? userId = null,
        [FromQuery] DateTimeOffset? dateFrom = null,
        [FromQuery] DateTimeOffset? dateTo = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool exportCsv = false,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 500);

        IQueryable<PickTask> query = _dbContext.PickTasks
            .AsNoTracking()
            .Where(x => x.Status == "Completed");

        if (itemId.HasValue)
        {
            query = query.Where(x => x.ItemId == itemId.Value);
        }

        if (!string.IsNullOrWhiteSpace(orderId))
        {
            var normalizedOrder = orderId.Trim();
            query = query.Where(x => x.OrderId == normalizedOrder);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var normalizedUser = userId.Trim();
            query = query.Where(x => x.UpdatedBy == normalizedUser || x.AssignedToUserId == normalizedUser);
        }

        if (dateFrom.HasValue)
        {
            query = query.Where(x => x.CompletedAt != null && x.CompletedAt >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(x => x.CompletedAt != null && x.CompletedAt <= dateTo.Value);
        }

        var sourceRows = exportCsv
            ? await query.OrderByDescending(x => x.CompletedAt).ToListAsync(cancellationToken)
            : await query
                .OrderByDescending(x => x.CompletedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

        var itemIds = sourceRows.Select(x => x.ItemId).Distinct().ToList();
        var locationIds = sourceRows
            .SelectMany(x => new[] { x.FromLocationId, x.ToLocationId })
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToList();

        var items = await _dbContext.Items
            .AsNoTracking()
            .Where(x => itemIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var locations = await _dbContext.Locations
            .AsNoTracking()
            .Where(x => locationIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var resultItems = sourceRows.Select(x =>
        {
            items.TryGetValue(x.ItemId, out var item);
            var fromCode = x.FromLocationId.HasValue && locations.TryGetValue(x.FromLocationId.Value, out var fromLocation)
                ? fromLocation.Code
                : null;
            var toCode = x.ToLocationId.HasValue && locations.TryGetValue(x.ToLocationId.Value, out var toLocation)
                ? toLocation.Code
                : null;

            return new PickHistoryItemDto(
                x.TaskId,
                x.OrderId,
                x.ItemId,
                item?.InternalSKU ?? string.Empty,
                item?.Name ?? string.Empty,
                x.Qty,
                x.PickedQty,
                x.AssignedToUserId,
                x.UpdatedBy,
                fromCode,
                toCode,
                x.CompletedAt);
        }).ToList();

        if (exportCsv)
        {
            return File(
                Encoding.UTF8.GetBytes(BuildPickHistoryCsv(resultItems)),
                "text/csv",
                "pick-history.csv");
        }

        var totalCount = await query.CountAsync(cancellationToken);
        return Ok(new PagedResponse<PickHistoryItemDto>(resultItems, totalCount, pageNumber, pageSize));
    }

    private static string BuildPickHistoryCsv(IReadOnlyCollection<PickHistoryItemDto> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("TaskId,OrderId,ItemId,ItemSku,ItemName,RequestedQty,PickedQty,AssignedToUserId,CompletedByUserId,FromLocation,ToLocation,CompletedAt");

        foreach (var row in rows)
        {
            sb.Append(row.TaskId).Append(',');
            sb.Append(EscapeCsv(row.OrderId)).Append(',');
            sb.Append(row.ItemId.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv(row.InternalSku)).Append(',');
            sb.Append(EscapeCsv(row.ItemName)).Append(',');
            sb.Append(row.Qty.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(row.PickedQty?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',');
            sb.Append(EscapeCsv(row.AssignedToUserId)).Append(',');
            sb.Append(EscapeCsv(row.CompletedBy)).Append(',');
            sb.Append(EscapeCsv(row.FromLocationCode)).Append(',');
            sb.Append(EscapeCsv(row.ToLocationCode)).Append(',');
            sb.Append(row.CompletedAt?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty);
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

    public sealed record CreatePickTaskRequest(
        string OrderId,
        int ItemId,
        decimal Qty,
        string? AssignedToUserId);

    public sealed record PickTaskCreatedResponse(
        Guid TaskId,
        string OrderId,
        int ItemId,
        decimal Qty,
        string Status,
        DateTimeOffset CreatedAt);

    public sealed record CompletePickTaskRequest(
        int FromLocationId,
        decimal PickedQty,
        int? LotId,
        string? ScannedBarcode,
        string? ScannedLocationBarcode,
        string? Notes);

    public sealed record CompletePickTaskResponse(
        Guid TaskId,
        Guid EventId,
        int ItemId,
        decimal PickedQty,
        int FromLocationId,
        int ToLocationId,
        string Status,
        DateTime Timestamp);

    public sealed record PickLocationSuggestion(
        string LocationCode,
        decimal AvailableQty,
        DateOnly? ExpiryDate,
        string? LotNumber);

    public sealed record PickLocationSuggestionResponse(
        Guid TaskId,
        int ItemId,
        string InternalSku,
        IReadOnlyList<PickLocationSuggestion> Locations);

    public sealed record PickHistoryItemDto(
        Guid TaskId,
        string OrderId,
        int ItemId,
        string InternalSku,
        string ItemName,
        decimal Qty,
        decimal? PickedQty,
        string? AssignedToUserId,
        string? CompletedBy,
        string? FromLocationCode,
        string? ToLocationCode,
        DateTimeOffset? CompletedAt);

    public sealed record PagedResponse<T>(
        IReadOnlyList<T> Items,
        int TotalCount,
        int PageNumber,
        int PageSize);
}
