using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Application.Commands;
using LKvitai.MES.Application.Queries;
using LKvitai.MES.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("api/projections")]
public sealed class ProjectionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProjectionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("rebuild")]
    public async Task<IActionResult> RebuildAsync(
        [FromBody] RebuildProjectionRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ProjectionName))
        {
            return ValidationFailure("Field 'projectionName' is required.");
        }

        var result = await _mediator.Send(new RebuildProjectionCommand
        {
            ProjectionName = request.ProjectionName.Trim(),
            Verify = true
        }, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return Failure(result);
    }

    [HttpPost("verify")]
    public async Task<IActionResult> VerifyAsync(
        [FromBody] VerifyProjectionRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ProjectionName))
        {
            return ValidationFailure("Field 'projectionName' is required.");
        }

        var result = await _mediator.Send(new VerifyProjectionQuery
        {
            ProjectionName = request.ProjectionName.Trim()
        }, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return Failure(result);
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

    public sealed record RebuildProjectionRequestDto(string ProjectionName);
    public sealed record VerifyProjectionRequestDto(string ProjectionName);
}
