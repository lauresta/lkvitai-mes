using LKvitai.MES.Contracts.Events;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using LKvitai.MES.Modules.Warehouse.Api.Configuration;
using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Domain;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using IDocumentStore = Marten.IDocumentStore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

/// <summary>
/// Issue to production: stock leaves a storage location for the PRODUCTION location in one step.
/// The movement crosses the stock boundary, so it is valued as an issue like a dispatch to a customer.
/// </summary>
[ApiController]
[Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
[Route("api/warehouse/v1/production-issues")]
public sealed class ProductionIssuesController : ControllerBase
{
    public const string ProductionLocation = "PRODUCTION";

    private readonly IWarehouseCodeProvider _warehouseCode;
    private readonly IBalanceGuardLockFactory _balanceLocks;
    private readonly WarehouseDbContext _dbContext;
    private readonly IDocumentStore _documentStore;
    private readonly ICurrentUserService _currentUser;

    public ProductionIssuesController(
        IWarehouseCodeProvider warehouseCode,
        IBalanceGuardLockFactory balanceLocks,
        WarehouseDbContext dbContext,
        IDocumentStore documentStore,
        ICurrentUserService currentUser)
    {
        _warehouseCode = warehouseCode;
        _balanceLocks = balanceLocks;
        _dbContext = dbContext;
        _documentStore = documentStore;
        _currentUser = currentUser;
    }

    [HttpPost]
    public async Task<IActionResult> IssueAsync([FromBody] ProductionIssueRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ItemId <= 0)
        {
            return ValidationFailure("Field 'itemId' is required.");
        }

        if (request.Qty <= 0m)
        {
            return ValidationFailure("Field 'qty' must be greater than 0.");
        }

        var reference = request.Reference?.Trim();
        if (reference is { Length: > 100 })
        {
            return ValidationFailure("Field 'reference' is longer than 100 characters.");
        }

        var item = await _dbContext.Items.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.ItemId, cancellationToken);
        if (item is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Item '{request.ItemId}' does not exist."));
        }

        var fromLocation = await _dbContext.Locations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.FromLocationId, cancellationToken);
        if (fromLocation is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Location '{request.FromLocationId}' does not exist."));
        }

        if (!PhysicalLocation.IsPhysical(fromLocation))
        {
            return ValidationFailure("Stock is issued to production from a physical storage location.");
        }

        if (!await _dbContext.Locations.AnyAsync(x => x.Code == ProductionLocation, cancellationToken))
        {
            return UnprocessableFailure("The PRODUCTION location does not exist.");
        }

        var warehouse = _warehouseCode.Code;
        await using var balanceLock = await _balanceLocks.CreateAsync(cancellationToken);
        await balanceLock.AcquireAsync([StockLockKey.ForLocation(warehouse, fromLocation.Code, item.InternalSKU)], cancellationToken);
        var available = await CommittedLocationStock.ReadAsync(_documentStore, warehouse, item.InternalSKU, fromLocation.Code, cancellationToken);
        if (available < request.Qty)
        {
            return UnprocessableFailure(
                $"Item {item.InternalSKU} at location {fromLocation.Code} has only {available} available. Cannot issue {request.Qty}.");
        }

        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        var evt = new StockMovedEvent
        {
            MovementId = Guid.NewGuid(),
            SKU = item.InternalSKU,
            Quantity = request.Qty,
            FromLocation = fromLocation.Code,
            ToLocation = ProductionLocation,
            MovementType = MovementType.ProductionIssue,
            OperatorId = Guid.Empty,
            Reason = ProductionIssueReason.Compose(reference, _currentUser.GetCurrentUserId(), notes),
            Timestamp = DateTime.UtcNow
        };

        IReadOnlyList<StockValueChanged> valueEvents = [];
        await StockValueRetry.RunAsync(async () =>
        {
            await using var session = _documentStore.LightweightSession();
            session.Events.Append(StockLedgerStreamId.For(warehouse, fromLocation.Code, item.InternalSKU), evt);
            valueEvents = await StockValueWriter.AppendAsync(session, _dbContext, [StockValueRequest.ForMovement(
                item.InternalSKU, request.Qty, fromLocation.Code, ProductionLocation,
                new StockValueDocument("ProductionIssue", evt.MovementId.ToString("N"), reference, notes))], cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
        }, cancellationToken);
        await StockValueWriter.ProjectAsync(_dbContext, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, valueEvents, cancellationToken);
        await balanceLock.CommitAsync(cancellationToken);

        return Ok(new ProductionIssueDto(
            evt.MovementId, item.Id, item.InternalSKU, item.Name, request.Qty, item.BaseUoM,
            fromLocation.Id, fromLocation.Code, reference, _currentUser.GetCurrentUserId(), notes, evt.Timestamp));
    }

    /// <summary>Issues to production, newest first: movements into PRODUCTION recorded by this endpoint.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAsync(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int? itemId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 500);
        var since = (from ?? DateTimeOffset.UtcNow.AddDays(-30)).UtcDateTime;
        var until = (to ?? DateTimeOffset.UtcNow.AddDays(1)).UtcDateTime;

        Item? filterItem = null;
        if (itemId.HasValue)
        {
            filterItem = await _dbContext.Items.AsNoTracking().FirstOrDefaultAsync(x => x.Id == itemId.Value, cancellationToken);
            if (filterItem is null)
            {
                return Ok(new PagedResponse<ProductionIssueDto>([], 0, pageNumber, pageSize));
            }
        }

        await using var session = _documentStore.QuerySession();
        var events = (await CommittedEventData.ReadAsync<StockMovedEvent>(session, filterItem?.InternalSKU, cancellationToken))
            .Where(x => string.Equals(x.MovementType, MovementType.ProductionIssue, StringComparison.OrdinalIgnoreCase)
                        && x.Timestamp >= since && x.Timestamp <= until)
            .OrderByDescending(x => x.Timestamp)
            .ToList();

        var page = events.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        var skus = page.Select(x => x.SKU.ToLower()).Distinct().ToList();
        var items = (await _dbContext.Items.AsNoTracking().Where(x => skus.Contains(x.InternalSKU.ToLower())).ToListAsync(cancellationToken))
            .GroupBy(x => x.InternalSKU, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var codes = page.Select(x => x.FromLocation).Distinct().ToList();
        var locations = (await _dbContext.Locations.AsNoTracking().Where(x => codes.Contains(x.Code)).ToListAsync(cancellationToken))
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var rows = page.Select(x =>
        {
            items.TryGetValue(x.SKU, out var item);
            locations.TryGetValue(x.FromLocation, out var location);
            var (reference, issuedBy, notes) = ProductionIssueReason.Parse(x.Reason);
            return new ProductionIssueDto(
                x.MovementId, item?.Id ?? 0, x.SKU, item?.Name ?? x.SKU, x.Quantity, item?.BaseUoM ?? string.Empty,
                location?.Id ?? 0, x.FromLocation, reference, issuedBy, notes, x.Timestamp);
        }).ToList();

        return Ok(new PagedResponse<ProductionIssueDto>(rows, events.Count, pageNumber, pageSize));
    }

    private ObjectResult ValidationFailure(string detail) => Problem(detail, StatusCodes.Status400BadRequest);

    private ObjectResult UnprocessableFailure(string detail) => Problem(detail, StatusCodes.Status422UnprocessableEntity);

    private ObjectResult Problem(string detail, int status)
        => new(ResultProblemDetailsMapper.ToProblemDetails(DomainErrorCodes.ValidationError, detail, HttpContext)) { StatusCode = status };

    private ObjectResult Failure(Result result)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(result, HttpContext);
        return new ObjectResult(problemDetails) { StatusCode = problemDetails.Status };
    }

    public sealed record ProductionIssueRequest(int ItemId, decimal Qty, int FromLocationId, string? Reference, string? Notes);

    public sealed record ProductionIssueDto(
        Guid MovementId,
        int ItemId,
        string ItemSku,
        string ItemName,
        decimal Qty,
        string BaseUoM,
        int FromLocationId,
        string FromLocationCode,
        string? Reference,
        string? IssuedBy,
        string? Notes,
        DateTime IssuedAt);

    public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int TotalCount, int PageNumber, int PageSize);
}
