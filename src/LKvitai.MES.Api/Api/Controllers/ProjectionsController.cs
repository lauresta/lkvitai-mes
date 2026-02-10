using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Application.Commands;
using LKvitai.MES.Application.Projections;
using LKvitai.MES.Application.Queries;
using LKvitai.MES.Infrastructure.Projections;
using LKvitai.MES.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
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
        var authFailure = RequireWarehouseAdmin();
        if (authFailure is not null)
        {
            return authFailure;
        }

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
        var authFailure = RequireWarehouseAdmin();
        if (authFailure is not null)
        {
            return authFailure;
        }

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

    private ObjectResult? RequireWarehouseAdmin()
    {
        var roleClaim = User.Claims.FirstOrDefault(c =>
            c.Type == ClaimTypes.Role || c.Type == "role")?.Value;
        var headerRole = HttpContext.Request.Headers["X-Warehouse-Role"].FirstOrDefault();

        var isAdmin = string.Equals(roleClaim, "WarehouseAdmin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerRole, "WarehouseAdmin", StringComparison.OrdinalIgnoreCase);

        if (isAdmin)
        {
            return null;
        }

        var unauthorized = string.IsNullOrWhiteSpace(roleClaim) && string.IsNullOrWhiteSpace(headerRole);
        return Failure(Result.Fail(
            unauthorized ? DomainErrorCodes.Unauthorized : DomainErrorCodes.Forbidden,
            unauthorized
                ? "WarehouseAdmin role is required. Authenticate first."
                : "WarehouseAdmin role is required for this operation."));
    }

    public sealed record RebuildProjectionRequestDto(string ProjectionName, bool ResetProgress = false);
    public sealed record VerifyProjectionRequestDto(string ProjectionName);
}
