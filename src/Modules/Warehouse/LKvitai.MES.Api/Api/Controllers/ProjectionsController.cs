using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Projections;
using LKvitai.MES.Modules.Warehouse.Application.Queries;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Infrastructure.Projections;
using LKvitai.MES.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Authorize(Policy = WarehousePolicies.AdminOnly)]
[Route("api/projections")]
[Route("api/warehouse/v1/admin/projections")]
public sealed class ProjectionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IProjectionRebuildService? _projectionRebuildService;
    private readonly IProjectionCleanupService? _projectionCleanupService;

    public ProjectionsController(
        IMediator mediator,
        IProjectionRebuildService? projectionRebuildService = null,
        IProjectionCleanupService? projectionCleanupService = null)
    {
        _mediator = mediator;
        _projectionRebuildService = projectionRebuildService;
        _projectionCleanupService = projectionCleanupService;
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
            CommandId = request.CommandId ?? Guid.NewGuid(),
            CorrelationId = ResolveCorrelationId(),
            ProjectionName = request.ProjectionName.Trim(),
            Verify = true,
            ResetProgress = request.ResetProgress
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

    [HttpPost("cleanup-shadows")]
    public async Task<IActionResult> CleanupShadowsAsync(CancellationToken cancellationToken = default)
    {
        if (_projectionCleanupService is null)
        {
            return Failure(Result.Fail(
                DomainErrorCodes.InternalError,
                "Projection cleanup service is not configured."));
        }

        var result = await _projectionCleanupService.CleanupShadowTablesAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("rebuild-status")]
    public async Task<IActionResult> GetRebuildStatusAsync(
        [FromQuery] string projectionName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectionName))
        {
            return ValidationFailure("Query parameter 'projectionName' is required.");
        }

        if (_projectionRebuildService is null)
        {
            return Failure(Result.Fail(
                DomainErrorCodes.InternalError,
                "Projection rebuild service is not configured."));
        }

        var status = await _projectionRebuildService.GetRebuildStatusAsync(
            projectionName.Trim(),
            cancellationToken);
        if (status is null)
        {
            return ValidationFailure("Query parameter 'projectionName' is required.");
        }

        return Ok(status);
    }

    private ObjectResult Failure(Result result)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(result, HttpContext);
        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };
    }

    private Guid ResolveCorrelationId()
    {
        var raw = HttpContext.Items[CorrelationIdMiddleware.HeaderName]?.ToString();
        return Guid.TryParse(raw, out var parsed) ? parsed : Guid.NewGuid();
    }

    public sealed record RebuildProjectionRequestDto(string ProjectionName, bool ResetProgress = false, Guid? CommandId = null);
    public sealed record VerifyProjectionRequestDto(string ProjectionName);
}
