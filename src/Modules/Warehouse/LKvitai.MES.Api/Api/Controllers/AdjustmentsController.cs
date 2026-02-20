using System.Diagnostics;
using System.Globalization;
using System.Text;
using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Application.Services;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using IDocumentStore = Marten.IDocumentStore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
[Route("api/warehouse/v1/adjustments")]
public sealed class AdjustmentsController : ControllerBase
{
    private const string DefaultWarehouseId = "WH1";

    private readonly WarehouseDbContext _dbContext;
    private readonly IDocumentStore _documentStore;
    private readonly ICurrentUserService _currentUserService;
    private readonly IReasonCodeService _reasonCodeService;

    public AdjustmentsController(
        WarehouseDbContext dbContext,
        IDocumentStore documentStore,
        ICurrentUserService currentUserService,
        IReasonCodeService reasonCodeService)
    {
        _dbContext = dbContext;
        _documentStore = documentStore;
        _currentUserService = currentUserService;
        _reasonCodeService = reasonCodeService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateAdjustmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ItemId <= 0)
        {
            return ValidationFailure("Field 'itemId' is required.");
        }

        if (request.LocationId <= 0)
        {
            return ValidationFailure("Field 'locationId' is required.");
        }

        if (request.QtyDelta == 0m)
        {
            return ValidationFailure("Field 'qtyDelta' cannot be zero.");
        }

        if (string.IsNullOrWhiteSpace(request.ReasonCode))
        {
            return ValidationFailure("Field 'reasonCode' is required.");
        }

        var item = await _dbContext.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ItemId, cancellationToken);
        if (item is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Item '{request.ItemId}' does not exist."));
        }

        var location = await _dbContext.Locations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.LocationId, cancellationToken);
        if (location is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Location '{request.LocationId}' does not exist."));
        }

        var normalizedReasonCode = request.ReasonCode.Trim().ToUpperInvariant();
        var reasonExists = await _dbContext.AdjustmentReasonCodes
            .AsNoTracking()
            .AnyAsync(
                x => x.Code == normalizedReasonCode &&
                     x.Active &&
                     x.Category == ReasonCategory.ADJUSTMENT,
                cancellationToken);
        if (!reasonExists)
        {
            return ValidationFailure($"ReasonCode '{request.ReasonCode}' does not exist or is inactive.");
        }

        if (request.LotId.HasValue)
        {
            var lotExists = await _dbContext.Lots
                .AsNoTracking()
                .AnyAsync(x => x.Id == request.LotId.Value && x.ItemId == request.ItemId, cancellationToken);
            if (!lotExists)
            {
                return ValidationFailure(
                    $"Lot '{request.LotId.Value}' does not exist for Item '{request.ItemId}'.");
            }
        }

        await using var querySession = _documentStore.QuerySession();
        var currentQty = await Marten.QueryableExtensions.SumAsync(
            querySession.Query<AvailableStockView>()
                .Where(x => x.WarehouseId == DefaultWarehouseId && x.Location == location.Code && x.SKU == item.InternalSKU),
            x => x.OnHandQty,
            cancellationToken);

        var newQty = currentQty + request.QtyDelta;
        var warning = newQty < 0m
            ? $"Negative stock warning: resulting quantity for {item.InternalSKU} at {location.Code} will be {newQty}."
            : null;

        var adjustmentId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var evt = new StockAdjustedEvent
        {
            AggregateId = adjustmentId,
            UserId = _currentUserService.GetCurrentUserId(),
            TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            WarehouseId = DefaultWarehouseId,
            AdjustmentId = adjustmentId,
            ItemId = item.Id,
            SKU = item.InternalSKU,
            LocationId = location.Id,
            Location = location.Code,
            LotId = request.LotId,
            LotNumber = request.LotId.HasValue
                ? await _dbContext.Lots
                    .AsNoTracking()
                    .Where(x => x.Id == request.LotId.Value)
                    .Select(x => x.LotNumber)
                    .FirstOrDefaultAsync(cancellationToken)
                : null,
            QtyDelta = request.QtyDelta,
            ReasonCode = normalizedReasonCode,
            Notes = request.Notes,
            Timestamp = now
        };

        await using (var session = _documentStore.LightweightSession())
        {
            session.Events.Append($"stock-adjustment:{adjustmentId:N}", evt);
            await session.SaveChangesAsync(cancellationToken);
        }

        var usageResult = await _reasonCodeService.IncrementUsageAsync(
            normalizedReasonCode,
            ReasonCategory.ADJUSTMENT,
            cancellationToken);
        if (!usageResult.IsSuccess)
        {
            return ValidationFailure(usageResult.ErrorDetail ?? usageResult.Error);
        }

        return Ok(new CreateAdjustmentResponse(
            adjustmentId,
            evt.EventId,
            item.Id,
            location.Id,
            request.QtyDelta,
            normalizedReasonCode,
            evt.UserId,
            now,
            warning));
    }

    [HttpGet]
    public async Task<IActionResult> GetAsync(
        [FromQuery] int? itemId = null,
        [FromQuery] int? locationId = null,
        [FromQuery] string? reasonCode = null,
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

        await using var querySession = _documentStore.QuerySession();
        IQueryable<AdjustmentHistoryView> query = querySession.Query<AdjustmentHistoryView>();

        if (itemId.HasValue)
        {
            query = query.Where(x => x.ItemId == itemId.Value);
        }

        if (locationId.HasValue)
        {
            query = query.Where(x => x.LocationId == locationId.Value);
        }

        if (!string.IsNullOrWhiteSpace(reasonCode))
        {
            var normalizedReason = reasonCode.Trim();
            query = query.Where(x => x.ReasonCode == normalizedReason);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var normalizedUser = userId.Trim();
            query = query.Where(x => x.UserId == normalizedUser);
        }

        if (dateFrom.HasValue)
        {
            query = query.Where(x => x.Timestamp >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(x => x.Timestamp <= dateTo.Value);
        }

        if (exportCsv)
        {
            var rows = await Marten.QueryableExtensions.ToListAsync(
                query.OrderByDescending(x => x.Timestamp),
                cancellationToken);

            var csv = BuildCsv(rows);
            return File(Encoding.UTF8.GetBytes(csv), "text/csv", "adjustments-history.csv");
        }

        var totalCount = await Marten.QueryableExtensions.CountAsync(query, cancellationToken);
        var items = await Marten.QueryableExtensions.ToListAsync(
            query.OrderByDescending(x => x.Timestamp)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize),
            cancellationToken);

        return Ok(new PagedResponse<AdjustmentHistoryItemDto>(
            items.Select(x => new AdjustmentHistoryItemDto(
                x.AdjustmentId,
                x.ItemId,
                x.SKU,
                x.ItemName,
                x.LocationId,
                x.LocationCode ?? x.Location,
                x.QtyDelta,
                x.ReasonCode,
                x.Notes,
                x.UserId,
                x.UserName,
                x.Timestamp)).ToList(),
            totalCount,
            pageNumber,
            pageSize));
    }

    private static string BuildCsv(IReadOnlyCollection<AdjustmentHistoryView> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("AdjustmentId,ItemId,SKU,LocationId,LocationCode,QtyDelta,ReasonCode,UserId,Timestamp,Notes");

        foreach (var row in rows)
        {
            sb.Append(row.AdjustmentId).Append(',');
            sb.Append(row.ItemId).Append(',');
            sb.Append(EscapeCsv(row.SKU)).Append(',');
            sb.Append(row.LocationId?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',');
            sb.Append(EscapeCsv(row.LocationCode ?? row.Location)).Append(',');
            sb.Append(row.QtyDelta.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv(row.ReasonCode)).Append(',');
            sb.Append(EscapeCsv(row.UserId)).Append(',');
            sb.Append(row.Timestamp.ToString("O", CultureInfo.InvariantCulture)).Append(',');
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

    private ObjectResult Failure(Result result)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(result, HttpContext);
        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };
    }

    public sealed record CreateAdjustmentRequest(
        int ItemId,
        int LocationId,
        decimal QtyDelta,
        string ReasonCode,
        string? Notes,
        int? LotId);

    public sealed record CreateAdjustmentResponse(
        Guid AdjustmentId,
        Guid EventId,
        int ItemId,
        int LocationId,
        decimal QtyDelta,
        string ReasonCode,
        string UserId,
        DateTime Timestamp,
        string? Warning);

    public sealed record AdjustmentHistoryItemDto(
        Guid AdjustmentId,
        int ItemId,
        string ItemSku,
        string? ItemName,
        int? LocationId,
        string LocationCode,
        decimal QtyDelta,
        string ReasonCode,
        string? Notes,
        string UserId,
        string? UserName,
        DateTimeOffset Timestamp);

    public sealed record PagedResponse<T>(
        IReadOnlyList<T> Items,
        int TotalCount,
        int PageNumber,
        int PageSize);
}
