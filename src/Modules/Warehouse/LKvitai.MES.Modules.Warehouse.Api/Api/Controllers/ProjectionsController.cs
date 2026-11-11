using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Projections;
using LKvitai.MES.Modules.Warehouse.Application.Queries;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Projections;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using LKvitai.MES.Contracts.ReadModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Authorize(Policy = WarehousePolicies.AdminOnly)]
[Route("api/projections")]
[Route("api/warehouse/v1/admin/projections")]
public sealed class ProjectionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IOnHandValueRebuildService? _onHandValueRebuild;
    private readonly IProjectionRebuildService? _projectionRebuildService;
    private readonly IProjectionCleanupService? _projectionCleanupService;

    public ProjectionsController(
        IMediator mediator,
        IProjectionRebuildService? projectionRebuildService = null,
        IProjectionCleanupService? projectionCleanupService = null,
        IOnHandValueRebuildService? onHandValueRebuild = null)
    {
        _mediator = mediator;
        _onHandValueRebuild = onHandValueRebuild;
        _projectionRebuildService = projectionRebuildService;
        _projectionCleanupService = projectionCleanupService;
    }

    [HttpPost("on-hand-value/preview")]
    public async Task<IActionResult> PreviewOnHandValueAsync(CancellationToken cancellationToken = default)
        => Ok(await _onHandValueRebuild!.PreviewAsync(cancellationToken));

    [HttpPost("on-hand-value/apply")]
    public async Task<IActionResult> ApplyOnHandValueAsync(
        [FromBody] OnHandValueApply request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 2000 ||
            string.IsNullOrWhiteSpace(request.ProductionChecksum) || string.IsNullOrWhiteSpace(request.ShadowChecksum))
            return ValidationFailure("Both checksums and a reason (1–2000 characters) are required.");
        var actor = new ProjectionAuditActor(
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.Identity?.Name ?? "unknown",
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            Request.Headers.UserAgent.ToString());
        return await _onHandValueRebuild!.ApplyAsync(request, actor, cancellationToken)
            ? Ok(new { applied = true })
            : Conflict(new ProblemDetails { Status = 409, Title = "Projection data changed",
                Detail = "Data changed. Run the preview again." });
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
