using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("api/sales-orders")]
[Route("api/warehouse/v1/sales-orders")]
public sealed class SalesOrdersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly WarehouseDbContext _dbContext;

    public SalesOrdersController(IMediator mediator, WarehouseDbContext dbContext)
    {
        _mediator = mediator;
        _dbContext = dbContext;
    }

    [HttpPost]
    [Authorize(Policy = WarehousePolicies.SalesAdminOrManager)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateSalesOrderRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        var orderId = Guid.NewGuid();
        var command = new CreateSalesOrderCommand
        {
            CommandId = request.CommandId == Guid.Empty ? Guid.NewGuid() : request.CommandId,
            CorrelationId = ResolveCorrelationId(),
            SalesOrderId = orderId,
            CustomerId = request.CustomerId,
            ShippingAddress = request.ShippingAddress?.ToDomain(),
            RequestedDeliveryDate = request.RequestedDeliveryDate,
            Lines = request.Lines.Select(x => new SalesOrderLineCommand
            {
                ItemId = x.ItemId,
                Qty = x.Qty,
                UnitPrice = x.UnitPrice
            }).ToList()
        };

        var result = await _mediator.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        var response = await LoadSalesOrderResponseAsync(orderId, cancellationToken);
        if (response is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, "Sales order was not created."));
        }

        return CreatedAtAction(nameof(GetByIdAsync), new { id = response.Id }, response);
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = WarehousePolicies.SalesAdminOrManager)]
    public async Task<IActionResult> SubmitAsync(
        Guid id,
        [FromBody] CommandRequest? request,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new SubmitSalesOrderCommand
        {
            CommandId = request?.CommandId is { } commandId && commandId != Guid.Empty
                ? commandId
                : Guid.NewGuid(),
            CorrelationId = ResolveCorrelationId(),
            SalesOrderId = id
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        var response = await LoadSalesOrderResponseAsync(id, cancellationToken);
        return response is null
            ? Failure(Result.Fail(DomainErrorCodes.NotFound, "Sales order not found."))
            : Ok(response);
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> ApproveAsync(
        Guid id,
        [FromBody] CommandRequest? request,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ApproveSalesOrderCommand
        {
            CommandId = request?.CommandId is { } commandId && commandId != Guid.Empty
                ? commandId
                : Guid.NewGuid(),
            CorrelationId = ResolveCorrelationId(),
            SalesOrderId = id
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        var response = await LoadSalesOrderResponseAsync(id, cancellationToken);
        return response is null
            ? Failure(Result.Fail(DomainErrorCodes.NotFound, "Sales order not found."))
            : Ok(response);
    }

    [HttpPost("{id:guid}/allocate")]
    [Authorize(Policy = WarehousePolicies.SalesAdminOrManager)]
    public async Task<IActionResult> AllocateAsync(
        Guid id,
        [FromBody] CommandRequest? request,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new AllocateSalesOrderCommand
        {
            CommandId = request?.CommandId is { } commandId && commandId != Guid.Empty
                ? commandId
                : Guid.NewGuid(),
            CorrelationId = ResolveCorrelationId(),
            SalesOrderId = id
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        var reservationId = await _dbContext.SalesOrders
            .Where(x => x.Id == id)
            .Select(x => x.ReservationId)
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new AllocationResponse(reservationId));
    }

    [HttpPost("{id:guid}/release")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> ReleaseAsync(
        Guid id,
        [FromBody] CommandRequest? request,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ReleaseSalesOrderCommand
        {
            CommandId = request?.CommandId is { } commandId && commandId != Guid.Empty
                ? commandId
                : Guid.NewGuid(),
            CorrelationId = ResolveCorrelationId(),
            SalesOrderId = id
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        var response = await LoadSalesOrderResponseAsync(id, cancellationToken);
        return response is null
            ? Failure(Result.Fail(DomainErrorCodes.NotFound, "Sales order not found."))
            : Ok(response);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = WarehousePolicies.SalesAdminOrManager)]
    public async Task<IActionResult> CancelAsync(
        Guid id,
        [FromBody] CancelSalesOrderRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        var result = await _mediator.Send(new CancelSalesOrderCommand
        {
            CommandId = request.CommandId == Guid.Empty ? Guid.NewGuid() : request.CommandId,
            CorrelationId = ResolveCorrelationId(),
            SalesOrderId = id,
            Reason = request.Reason
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        var response = await LoadSalesOrderResponseAsync(id, cancellationToken);
        return response is null
            ? Failure(Result.Fail(DomainErrorCodes.NotFound, "Sales order not found."))
            : Ok(response);
    }

    [HttpGet]
    [Authorize(Policy = WarehousePolicies.SalesAdminOrManager)]
    public async Task<IActionResult> GetListAsync(
        [FromQuery] string? status,
        [FromQuery] Guid? customerId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 200)
        {
            return ValidationFailure("Invalid page or pageSize value.");
        }

        IQueryable<SalesOrder> query = _dbContext.SalesOrders
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Lines);

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<SalesOrderStatus>(status, true, out var parsedStatus))
            {
                return ValidationFailure("Invalid status filter value.");
            }

            query = query.Where(x => x.Status == parsedStatus);
        }

        if (customerId.HasValue)
        {
            query = query.Where(x => x.CustomerId == customerId.Value);
        }

        if (dateFrom.HasValue)
        {
            var from = DateOnly.FromDateTime(dateFrom.Value);
            query = query.Where(x => x.OrderDate >= from);
        }

        if (dateTo.HasValue)
        {
            var to = DateOnly.FromDateTime(dateTo.Value);
            query = query.Where(x => x.OrderDate <= to);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var response = items.Select(MapResponse).ToList();
        return Ok(new PagedResponse<SalesOrderResponse>(response, totalCount, page, pageSize));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = WarehousePolicies.SalesAdminOrManager)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await LoadSalesOrderResponseAsync(id, cancellationToken);
        if (response is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, "Sales order not found."));
        }

        return Ok(response);
    }

    private async Task<SalesOrderResponse?> LoadSalesOrderResponseAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await _dbContext.SalesOrders
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Item)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return order is null ? null : MapResponse(order);
    }

    private static SalesOrderResponse MapResponse(SalesOrder order)
    {
        var shipping = order.ShippingAddress is null
            ? null
            : new AddressDto(
                order.ShippingAddress.Street,
                order.ShippingAddress.City,
                order.ShippingAddress.State,
                order.ShippingAddress.ZipCode,
                order.ShippingAddress.Country);

        return new SalesOrderResponse(
            order.Id,
            order.OrderNumber,
            order.CustomerId,
            order.Customer?.Name ?? string.Empty,
            shipping,
            order.Status.ToString().ToUpperInvariant(),
            order.OrderDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            order.RequestedDeliveryDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            order.AllocatedAt?.UtcDateTime,
            order.ShippedAt?.UtcDateTime,
            order.Lines.Select(line => new SalesOrderLineResponse(
                line.Id,
                line.ItemId,
                line.Item?.InternalSKU ?? string.Empty,
                line.Item?.Name ?? string.Empty,
                line.OrderedQty,
                line.AllocatedQty,
                line.PickedQty,
                line.ShippedQty,
                line.UnitPrice,
                line.LineAmount
            )).ToList(),
            order.ReservationId,
            order.OutboundOrderId,
            order.TotalAmount);
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

    public sealed record CreateSalesOrderRequest(
        Guid CommandId,
        Guid CustomerId,
        AddressDto? ShippingAddress,
        DateTime? RequestedDeliveryDate,
        IReadOnlyList<SalesOrderLineRequest> Lines);

    public sealed record SalesOrderLineRequest(int ItemId, decimal Qty, decimal UnitPrice);

    public sealed record CommandRequest(Guid CommandId);

    public sealed record CancelSalesOrderRequest(Guid CommandId, string Reason);

    public sealed record AddressDto(
        string Street,
        string City,
        string State,
        string ZipCode,
        string Country)
    {
        public Address ToDomain()
            => new()
            {
                Street = Street,
                City = City,
                State = State,
                ZipCode = ZipCode,
                Country = Country
            };
    }

    public sealed record SalesOrderResponse(
        Guid Id,
        string OrderNumber,
        Guid CustomerId,
        string CustomerName,
        AddressDto? ShippingAddress,
        string Status,
        DateTime OrderDate,
        DateTime? RequestedDeliveryDate,
        DateTime? AllocatedAt,
        DateTime? ShippedAt,
        IReadOnlyList<SalesOrderLineResponse> Lines,
        Guid? ReservationId,
        Guid? OutboundOrderId,
        decimal TotalAmount);

    public sealed record SalesOrderLineResponse(
        Guid Id,
        int ItemId,
        string ItemSku,
        string ItemDescription,
        decimal OrderedQty,
        decimal AllocatedQty,
        decimal PickedQty,
        decimal ShippedQty,
        decimal UnitPrice,
        decimal LineAmount);

    public sealed record AllocationResponse(Guid? ReservationId);

    public sealed record PagedResponse<T>(
        IReadOnlyList<T> Items,
        int TotalCount,
        int Page,
        int PageSize);
}
