using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Application.Commands;
using LKvitai.MES.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/cycle-counts")]
public sealed class CycleCountsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly WarehouseDbContext _dbContext;

    public CycleCountsController(IMediator mediator, WarehouseDbContext dbContext)
    {
        _mediator = mediator;
        _dbContext = dbContext;
    }

    [HttpGet]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetListAsync(CancellationToken cancellationToken = default)
    {
        var cycleCounts = await _dbContext.CycleCounts
            .AsNoTracking()
            .Include(x => x.Lines)
            .OrderByDescending(x => x.ScheduledDate)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(cycleCounts.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cycleCount = await _dbContext.CycleCounts
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (cycleCount is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, "Cycle count not found."));
        }

        return Ok(ToResponse(cycleCount));
    }

    [HttpPost("schedule")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> ScheduleAsync(
        [FromBody] ScheduleCycleCountRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        var commandId = request.CommandId == Guid.Empty ? Guid.NewGuid() : request.CommandId;
        var result = await _mediator.Send(new ScheduleCycleCountCommand
        {
            CommandId = commandId,
            CorrelationId = ResolveCorrelationId(),
            ScheduledDate = request.ScheduledDate,
            AbcClass = request.AbcClass,
            LocationIds = request.LocationIds ?? Array.Empty<int>(),
            AssignedOperator = request.AssignedOperator
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        var cycleCount = await _dbContext.CycleCounts
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.ScheduleCommandId == commandId, cancellationToken);
        if (cycleCount is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.InternalError, "Cycle count not found after scheduling."));
        }

        return Ok(ToResponse(cycleCount));
    }

    [HttpPost("{id:guid}/record-count")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> RecordCountAsync(
        Guid id,
        [FromBody] RecordCountRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }

        var result = await _mediator.Send(new RecordCountCommand
        {
            CommandId = request.CommandId == Guid.Empty ? Guid.NewGuid() : request.CommandId,
            CorrelationId = ResolveCorrelationId(),
            CycleCountId = id,
            LocationId = request.LocationId,
            ItemId = request.ItemId,
            PhysicalQty = request.PhysicalQty,
            Reason = request.Reason,
            CountedBy = request.CountedBy ?? string.Empty
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        var cycleCount = await _dbContext.CycleCounts
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (cycleCount is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, "Cycle count not found."));
        }

        return Ok(ToResponse(cycleCount));
    }

    [HttpPost("{id:guid}/apply-adjustment")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> ApplyAdjustmentAsync(
        Guid id,
        [FromBody] ApplyAdjustmentRequest? request,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new ApplyAdjustmentCommand
        {
            CommandId = request?.CommandId == Guid.Empty ? Guid.NewGuid() : request?.CommandId ?? Guid.NewGuid(),
            CorrelationId = ResolveCorrelationId(),
            CycleCountId = id,
            ApproverId = request?.ApproverId
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        var cycleCount = await _dbContext.CycleCounts
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (cycleCount is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, "Cycle count not found."));
        }

        return Ok(ToResponse(cycleCount));
    }

    private static CycleCountResponse ToResponse(CycleCount cycleCount)
    {
        return new CycleCountResponse(
            cycleCount.Id,
            cycleCount.CountNumber,
            cycleCount.Status.ToString().ToUpperInvariant(),
            cycleCount.ScheduledDate,
            cycleCount.AbcClass,
            cycleCount.AssignedOperator,
            cycleCount.StartedAt,
            cycleCount.CompletedAt,
            cycleCount.CreatedBy,
            cycleCount.CreatedAt,
            cycleCount.CountedBy,
            cycleCount.ApprovedBy,
            cycleCount.Lines.Select(x => x.LocationId).Distinct().OrderBy(x => x).ToList(),
            cycleCount.Lines.Select(x => new CycleCountLineResponse(
                x.LocationId,
                x.ItemId,
                x.SystemQty,
                x.PhysicalQty,
                x.Delta,
                x.Status.ToString().ToUpperInvariant(),
                x.Reason)).ToList());
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

    public sealed record ScheduleCycleCountRequest(
        Guid CommandId,
        DateTimeOffset ScheduledDate,
        string AbcClass,
        string AssignedOperator,
        IReadOnlyList<int>? LocationIds = null);

    public sealed record RecordCountRequest(
        Guid CommandId,
        int LocationId,
        int ItemId,
        decimal PhysicalQty,
        string? Reason,
        string? CountedBy);

    public sealed record ApplyAdjustmentRequest(Guid CommandId, string? ApproverId);

    public sealed record CycleCountLineResponse(
        int LocationId,
        int ItemId,
        decimal SystemQty,
        decimal PhysicalQty,
        decimal Delta,
        string Status,
        string? Reason);

    public sealed record CycleCountResponse(
        Guid Id,
        string CountNumber,
        string Status,
        DateTimeOffset ScheduledDate,
        string AbcClass,
        string AssignedOperator,
        DateTimeOffset? StartedAt,
        DateTimeOffset? CompletedAt,
        string? CreatedBy,
        DateTimeOffset CreatedAt,
        string? CountedBy,
        string? ApprovedBy,
        IReadOnlyList<int> LocationIds,
        IReadOnlyList<CycleCountLineResponse> Lines);
}
