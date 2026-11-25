using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Application.Queries;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
[Route("api/reservations")]
[Route("api/warehouse/v1/reservations")]
public sealed class ReservationsController : ControllerBase
{
    private const int MaxActiveReservations = 500;

    private readonly IMediator _mediator;
    private readonly IReservationRepository _reservationRepository;

    public ReservationsController(
        IMediator mediator,
        IReservationRepository reservationRepository)
    {
        _mediator = mediator;
        _reservationRepository = reservationRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetReservationsAsync(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            return ValidationFailure("Parameter 'page' must be greater than or equal to 1.");
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return ValidationFailure("Parameter 'pageSize' must be between 1 and 100.");
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            status.Trim().ToUpperInvariant() is not ("ALLOCATED" or "PICKING"))
        {
            return ValidationFailure("Parameter 'status' must be one of: ALLOCATED, PICKING.");
        }

        var result = await _mediator.Send(new SearchReservationsQuery
        {
            Status = status,
            Page = page,
            PageSize = pageSize
        }, cancellationToken);

        return this.ToApiResult(result);
    }

    /// <summary>
    /// Active (allocated / picking) reservation lines of one item, with the owning sales order and customer.
    /// Returns an empty list without loading reservation streams when no stock row of the item is reserved.
    /// </summary>
    [HttpGet("by-item/{itemId:int}")]
    public async Task<IActionResult> GetByItemAsync(
        int itemId,
        [FromServices] WarehouseDbContext dbContext,
        [FromServices] Marten.IDocumentStore documentStore,
        [FromServices] IReservationReadModelQueryService readModels,
        CancellationToken cancellationToken = default)
    {
        var sku = await dbContext.Items
            .AsNoTracking()
            .Where(x => x.Id == itemId)
            .Select(x => x.InternalSKU)
            .FirstOrDefaultAsync(cancellationToken);
        if (sku is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Item with ID {itemId} does not exist."));
        }

        if (!await HasReservedStockAsync(documentStore, sku, cancellationToken))
        {
            return Ok(Array.Empty<ItemReservationRowDto>());
        }

        var summaries = await readModels.GetReservationPageAsync(null, 0, MaxActiveReservations, cancellationToken);
        var loaded = await Task.WhenAll(summaries.Select(x => _reservationRepository.LoadAsync(x.ReservationId, cancellationToken)));
        var lines = summaries
            .Zip(loaded, (summary, stream) => ToLineSources(summary, stream.Reservation))
            .SelectMany(x => x)
            .Where(x => string.Equals(x.Sku, sku, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var orders = await LoadOrdersAsync(dbContext, lines.Select(x => x.ReservationId).Distinct().ToList(), cancellationToken);
        return Ok(ItemReservationRows.Build(sku, lines, orders));
    }

    private static async Task<bool> HasReservedStockAsync(Marten.IDocumentStore documentStore, string sku, CancellationToken cancellationToken)
    {
        await using var session = documentStore.QuerySession();
        return await Marten.QueryableExtensions.AnyAsync(
            session.Query<AvailableStockView>()
                .Where(x => x.SKU == sku && (x.ReservedQty > 0m || x.SalesReservedQty > 0m || x.HardLockedQty > 0m)),
            cancellationToken);
    }

    private static IEnumerable<ReservationLineSource> ToLineSources(ReservationSummaryView summary, Reservation? reservation)
        => reservation is null
            ? Enumerable.Empty<ReservationLineSource>()
            : reservation.Lines.Select(line => new ReservationLineSource(
                summary.ReservationId,
                summary.Purpose,
                summary.Status,
                summary.CreatedAt,
                line.SKU,
                line.Location,
                line.AllocatedQuantity > 0m ? line.AllocatedQuantity : line.RequestedQuantity));

    private static async Task<IReadOnlyDictionary<Guid, ReservationOrderRef>> LoadOrdersAsync(
        WarehouseDbContext dbContext,
        IReadOnlyCollection<Guid> reservationIds,
        CancellationToken cancellationToken)
    {
        if (reservationIds.Count == 0)
        {
            return new Dictionary<Guid, ReservationOrderRef>();
        }

        var orders = await dbContext.SalesOrders
            .AsNoTracking()
            .Include(x => x.Customer)
            .Where(x => x.ReservationId != null && reservationIds.Contains(x.ReservationId.Value))
            .ToListAsync(cancellationToken);

        return orders
            .GroupBy(x => x.ReservationId!.Value)
            .ToDictionary(
                x => x.Key,
                x => x.Select(order => new ReservationOrderRef(order.Id, order.OrderNumber, order.Customer?.Name, order.Status.ToString())).First());
    }

    [HttpPost("{id:guid}/start-picking")]
    public async Task<IActionResult> StartPickingAsync(
        Guid id,
        [FromBody] StartPickingRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        if (request is not null &&
            request.ReservationId != Guid.Empty &&
            request.ReservationId != id)
        {
            return ValidationFailure("Route reservation id and payload reservationId must match.");
        }

        var result = await _mediator.Send(new StartPickingCommand
        {
            CommandId = request?.CommandId is { } commandId && commandId != Guid.Empty
                ? commandId
                : Guid.NewGuid(),
            CorrelationId = ResolveCorrelationId(),
            ReservationId = id,
            OperatorId = Guid.Empty
        }, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(new StartPickingResponseDto(true, "Picking started"));
        }

        return Failure(result);
    }

    [HttpPost("{id:guid}/pick")]
    public async Task<IActionResult> PickAsync(
        Guid id,
        [FromBody] PickRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        if (request.ReservationId != Guid.Empty && request.ReservationId != id)
        {
            return ValidationFailure("Route reservation id and payload reservationId must match.");
        }

        if (request.HuId == Guid.Empty)
        {
            return ValidationFailure("Field 'huId' is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Sku))
        {
            return ValidationFailure("Field 'sku' is required.");
        }

        if (request.Quantity <= 0m)
        {
            return ValidationFailure("Field 'quantity' must be greater than 0.");
        }

        var loaded = await _reservationRepository.LoadAsync(id, cancellationToken);
        if (loaded.Reservation is null)
        {
            return Failure(Result.Fail(
                DomainErrorCodes.NotFound,
                $"Reservation {id} was not found."));
        }

        var line = loaded.Reservation.Lines.FirstOrDefault(x =>
            string.Equals(x.SKU, request.Sku, StringComparison.OrdinalIgnoreCase) &&
            x.AllocatedHUs.Contains(request.HuId));

        if (line is null)
        {
            return ValidationFailure(
                $"No allocated line found for SKU '{request.Sku}' and HU '{request.HuId}'.");
        }

        if (line.AllocatedQuantity > 0m && request.Quantity > line.AllocatedQuantity)
        {
            return ValidationFailure(
                $"Quantity {request.Quantity} exceeds allocated quantity {line.AllocatedQuantity} for SKU '{request.Sku}'.");
        }

        var result = await _mediator.Send(new PickStockCommand
        {
            CommandId = request.CommandId != Guid.Empty ? request.CommandId : Guid.NewGuid(),
            CorrelationId = ResolveCorrelationId(),
            ReservationId = id,
            HandlingUnitId = request.HuId,
            SKU = request.Sku.Trim(),
            Quantity = request.Quantity,
            WarehouseId = line.WarehouseId,
            FromLocation = line.Location,
            OperatorId = Guid.Empty
        }, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(new PickResponseDto(true, "Pick completed"));
        }

        return Failure(result);
    }

    private ObjectResult Failure(Result result)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(result, HttpContext);
        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };
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

    private Guid ResolveCorrelationId()
    {
        var raw = HttpContext.Items[CorrelationIdMiddleware.HeaderName]?.ToString();
        return Guid.TryParse(raw, out var parsed) ? parsed : Guid.NewGuid();
    }

    public sealed record StartPickingRequestDto(Guid ReservationId, Guid? CommandId = null);
    public sealed record PickRequestDto(Guid ReservationId, Guid HuId, string Sku, decimal Quantity, Guid CommandId = default);
    public sealed record StartPickingResponseDto(bool Success, string Message);
    public sealed record PickResponseDto(bool Success, string Message);
}
