using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/outbound/orders")]
public sealed class OutboundOrdersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly WarehouseDbContext _dbContext;

    public OutboundOrdersController(IMediator mediator, WarehouseDbContext dbContext)
    {
        _mediator = mediator;
        _dbContext = dbContext;
    }

    [HttpPost("{id:guid}/pack")]
    [Authorize(Policy = WarehousePolicies.PackingOperatorOrManager)]
    public async Task<IActionResult> PackAsync(
        Guid id,
        [FromBody] PackOrderRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        var result = await _mediator.Send(new PackOrderCommand
        {
            CommandId = request.CommandId == Guid.Empty ? Guid.NewGuid() : request.CommandId,
            CorrelationId = ResolveCorrelationId(),
            OutboundOrderId = id,
            PackagingType = request.PackagingType,
            ScannedItems = request.ScannedItems.Select(x => new ScannedItemCommand
            {
                Barcode = x.Barcode,
                Qty = x.Qty
            }).ToList()
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        var shipment = await _dbContext.Shipments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.OutboundOrderId == id, cancellationToken);

        if (shipment is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, "Shipment was not created."));
        }

        var handlingUnit = shipment.ShippingHandlingUnitId.HasValue
            ? await _dbContext.HandlingUnits.AsNoTracking()
                .FirstOrDefaultAsync(x => x.HUId == shipment.ShippingHandlingUnitId.Value, cancellationToken)
            : null;

        return Ok(new PackOrderResponse(
            shipment.Id,
            shipment.ShipmentNumber,
            shipment.ShippingHandlingUnitId ?? Guid.Empty,
            handlingUnit?.LPN ?? string.Empty,
            $"/labels/preview?labelType=HU&lpn={Uri.EscapeDataString(handlingUnit?.LPN ?? string.Empty)}"));
    }

    [HttpGet("summary")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetSummaryAsync(
        [FromQuery] string? status,
        [FromQuery] string? customer,
        [FromQuery] DateTimeOffset? dateFrom,
        [FromQuery] DateTimeOffset? dateTo,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.OutboundOrderSummaries.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status.ToUpperInvariant());
        }

        if (!string.IsNullOrWhiteSpace(customer))
        {
            var customerFilter = customer.Trim();
            query = query.Where(x => x.CustomerName != null && x.CustomerName.Contains(customerFilter));
        }

        if (dateFrom.HasValue)
        {
            query = query.Where(x => x.OrderDate >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(x => x.OrderDate <= dateTo.Value);
        }

        var rows = await query
            .OrderByDescending(x => x.OrderDate)
            .Select(x => new OutboundOrderSummaryResponse(
                x.Id,
                x.OrderNumber,
                x.Type,
                x.Status,
                x.CustomerName,
                x.ItemCount,
                x.OrderDate,
                x.RequestedShipDate,
                x.PackedAt,
                x.ShippedAt,
                x.ShipmentId,
                x.ShipmentNumber,
                x.TrackingNumber))
            .ToListAsync(cancellationToken);

        return Ok(rows);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.OutboundOrders
            .AsNoTracking()
            .Include(x => x.Lines)
                .ThenInclude(x => x.Item)
            .Include(x => x.SalesOrder)
                .ThenInclude(x => x!.Customer)
            .Include(x => x.Shipment)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (order is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, "Outbound order not found."));
        }

        var response = new OutboundOrderDetailResponse(
            order.Id,
            order.OrderNumber,
            order.Type.ToString().ToUpperInvariant(),
            order.Status.ToString().ToUpperInvariant(),
            order.SalesOrder?.Customer?.Name,
            order.OrderDate,
            order.RequestedShipDate,
            order.PickedAt,
            order.PackedAt,
            order.ShippedAt,
            order.ReservationId,
            order.SalesOrderId,
            order.Shipment is null
                ? null
                : new OutboundShipmentInfoResponse(
                    order.Shipment.Id,
                    order.Shipment.ShipmentNumber,
                    order.Shipment.Status.ToString().ToUpperInvariant(),
                    order.Shipment.Carrier.ToString().ToUpperInvariant(),
                    order.Shipment.TrackingNumber,
                    order.Shipment.PackedAt,
                    order.Shipment.DispatchedAt),
            order.Lines
                .OrderBy(x => x.ItemId)
                .Select(x => new OutboundOrderDetailLineResponse(
                    x.Id,
                    x.ItemId,
                    x.Item?.InternalSKU ?? string.Empty,
                    x.Item?.Name ?? string.Empty,
                    x.Item?.PrimaryBarcode,
                    x.Qty,
                    x.PickedQty,
                    x.ShippedQty))
                .ToList());

        return Ok(response);
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

    public sealed record PackOrderRequest(
        Guid CommandId,
        IReadOnlyList<ScannedItemRequest> ScannedItems,
        string PackagingType = "BOX");

    public sealed record ScannedItemRequest(string Barcode, decimal Qty);

    public sealed record PackOrderResponse(
        Guid ShipmentId,
        string ShipmentNumber,
        Guid HandlingUnitId,
        string HandlingUnitCode,
        string LabelPreviewUrl);

    public sealed record OutboundOrderSummaryResponse(
        Guid Id,
        string OrderNumber,
        string Type,
        string Status,
        string? CustomerName,
        int ItemCount,
        DateTimeOffset OrderDate,
        DateTimeOffset? RequestedShipDate,
        DateTimeOffset? PackedAt,
        DateTimeOffset? ShippedAt,
        Guid? ShipmentId,
        string? ShipmentNumber,
        string? TrackingNumber);

    public sealed record OutboundOrderDetailResponse(
        Guid Id,
        string OrderNumber,
        string Type,
        string Status,
        string? CustomerName,
        DateTimeOffset OrderDate,
        DateTimeOffset? RequestedShipDate,
        DateTimeOffset? PickedAt,
        DateTimeOffset? PackedAt,
        DateTimeOffset? ShippedAt,
        Guid ReservationId,
        Guid? SalesOrderId,
        OutboundShipmentInfoResponse? Shipment,
        IReadOnlyList<OutboundOrderDetailLineResponse> Lines);

    public sealed record OutboundOrderDetailLineResponse(
        Guid Id,
        int ItemId,
        string ItemSku,
        string ItemName,
        string? PrimaryBarcode,
        decimal Qty,
        decimal PickedQty,
        decimal ShippedQty);

    public sealed record OutboundShipmentInfoResponse(
        Guid ShipmentId,
        string ShipmentNumber,
        string Status,
        string Carrier,
        string? TrackingNumber,
        DateTimeOffset? PackedAt,
        DateTimeOffset? DispatchedAt);
}
