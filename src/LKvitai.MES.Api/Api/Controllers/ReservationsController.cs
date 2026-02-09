using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Application.Queries;
using LKvitai.MES.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("api/reservations")]
public sealed class ReservationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReservationsController(IMediator mediator)
    {
        _mediator = mediator;
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
}
