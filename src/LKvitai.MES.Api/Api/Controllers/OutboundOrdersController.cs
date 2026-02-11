using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Application.Commands;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Api.Controllers;

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
}
