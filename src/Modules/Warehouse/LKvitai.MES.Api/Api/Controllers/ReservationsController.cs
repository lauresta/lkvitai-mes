using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Application.Queries;
using LKvitai.MES.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
[Route("api/reservations")]
[Route("api/warehouse/v1/reservations")]
public sealed class ReservationsController : ControllerBase
{
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
