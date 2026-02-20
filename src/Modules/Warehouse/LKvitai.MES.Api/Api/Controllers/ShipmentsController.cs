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
[Route("api/warehouse/v1/shipments")]
public sealed class ShipmentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly WarehouseDbContext _dbContext;

    public ShipmentsController(IMediator mediator, WarehouseDbContext dbContext)
    {
        _mediator = mediator;
        _dbContext = dbContext;
    }

    [HttpPost("{id:guid}/dispatch")]
    [Authorize(Policy = WarehousePolicies.DispatchClerkOrManager)]
    public async Task<IActionResult> DispatchAsync(
        Guid id,
        [FromBody] DispatchShipmentRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        var result = await _mediator.Send(new DispatchShipmentCommand
        {
            CommandId = request.CommandId == Guid.Empty ? Guid.NewGuid() : request.CommandId,
            CorrelationId = ResolveCorrelationId(),
            ShipmentId = id,
            Carrier = request.Carrier,
            VehicleId = request.VehicleId,
            DispatchTime = request.DispatchTime,
            ManualTrackingNumber = request.ManualTrackingNumber
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        var shipment = await _dbContext.Shipments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (shipment is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, "Shipment not found."));
        }

        return Ok(new DispatchShipmentResponse(
            shipment.Id,
            shipment.ShipmentNumber,
            shipment.Carrier.ToString().ToUpperInvariant(),
            shipment.TrackingNumber ?? string.Empty,
            shipment.DispatchedAt?.UtcDateTime ?? DateTime.UtcNow,
            User.Identity?.Name ?? "system"));
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
        var query = _dbContext.ShipmentSummaries.AsNoTracking().AsQueryable();
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
            query = query.Where(x => x.DispatchedAt == null || x.DispatchedAt >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(x => x.DispatchedAt == null || x.DispatchedAt <= dateTo.Value);
        }

        var rows = await query
            .OrderByDescending(x => x.PackedAt)
            .Select(x => new ShipmentSummaryResponse(
                x.Id,
                x.ShipmentNumber,
                x.OutboundOrderId,
                x.OutboundOrderNumber,
                x.CustomerName,
                x.Carrier,
                x.TrackingNumber,
                x.Status,
                x.PackedAt,
                x.DispatchedAt,
                x.DeliveredAt,
                x.PackedBy,
                x.DispatchedBy))
            .ToListAsync(cancellationToken);

        return Ok(rows);
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

    public sealed record DispatchShipmentRequest(
        Guid CommandId,
        string Carrier,
        string? VehicleId,
        DateTimeOffset? DispatchTime,
        string? ManualTrackingNumber);

    public sealed record DispatchShipmentResponse(
        Guid ShipmentId,
        string ShipmentNumber,
        string Carrier,
        string TrackingNumber,
        DateTime DispatchedAt,
        string DispatchedBy);

    public sealed record ShipmentSummaryResponse(
        Guid Id,
        string ShipmentNumber,
        Guid OutboundOrderId,
        string OutboundOrderNumber,
        string? CustomerName,
        string Carrier,
        string? TrackingNumber,
        string Status,
        DateTimeOffset? PackedAt,
        DateTimeOffset? DispatchedAt,
        DateTimeOffset? DeliveredAt,
        string? PackedBy,
        string? DispatchedBy);
}
