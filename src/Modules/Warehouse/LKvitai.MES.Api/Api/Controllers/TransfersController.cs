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
[Route("api/warehouse/v1/transfers")]
public sealed class TransfersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly WarehouseDbContext _dbContext;

    public TransfersController(IMediator mediator, WarehouseDbContext dbContext)
    {
        _mediator = mediator;
        _dbContext = dbContext;
    }

    [HttpGet]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetListAsync(
        [FromQuery] string? status,
        [FromQuery] DateTimeOffset? dateFrom,
        [FromQuery] DateTimeOffset? dateTo,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var query = _dbContext.Transfers
            .AsNoTracking()
            .Include(x => x.Lines)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase);
            if (!Enum.TryParse<LKvitai.MES.Modules.Warehouse.Domain.Entities.TransferStatus>(normalizedStatus, true, out var parsedStatus))
            {
                return ValidationFailure("Invalid status value.");
            }

            query = query.Where(x => x.Status == parsedStatus);
        }

        if (dateFrom.HasValue)
        {
            query = query.Where(x => x.RequestedAt >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(x => x.RequestedAt <= dateTo.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(x => x.RequestedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<TransferResponse>(
            rows.Select(ToResponse).ToList(),
            totalCount,
            pageNumber,
            pageSize));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var transfer = await _dbContext.Transfers
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (transfer is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, "Transfer not found."));
        }

        return Ok(ToResponse(transfer));
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> SubmitAsync(
        Guid id,
        [FromBody] SubmitTransferRequest? request,
        CancellationToken cancellationToken = default)
    {
        var commandId = request?.CommandId == Guid.Empty ? Guid.NewGuid() : request?.CommandId ?? Guid.NewGuid();

        var result = await _mediator.Send(new SubmitTransferCommand
        {
            CommandId = commandId,
            CorrelationId = ResolveCorrelationId(),
            TransferId = id
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        var transfer = await _dbContext.Transfers
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (transfer is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, "Transfer not found."));
        }

        return Ok(ToResponse(transfer));
    }

    [HttpPost]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateTransferRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ValidationFailure("Request body is required.");
        }
        if (request.Lines is null || request.Lines.Count == 0)
        {
            return ValidationFailure("At least one transfer line is required.");
        }

        var commandId = request.CommandId == Guid.Empty ? Guid.NewGuid() : request.CommandId;

        var result = await _mediator.Send(new CreateTransferCommand
        {
            CommandId = commandId,
            CorrelationId = ResolveCorrelationId(),
            FromWarehouse = request.FromWarehouse,
            ToWarehouse = request.ToWarehouse,
            RequestedBy = request.RequestedBy ?? string.Empty,
            Lines = request.Lines.Select(x => new TransferLineCommand
            {
                ItemId = x.ItemId,
                Qty = x.Qty,
                FromLocationId = x.FromLocationId,
                ToLocationId = x.ToLocationId,
                LotId = x.LotId
            }).ToList()
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        var transfer = await _dbContext.Transfers
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.CreateCommandId == commandId, cancellationToken);

        if (transfer is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.InternalError, "Transfer was not found after creation."));
        }

        return Ok(ToResponse(transfer));
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> ApproveAsync(
        Guid id,
        [FromBody] ApproveTransferRequest? request,
        CancellationToken cancellationToken = default)
    {
        var commandId = request?.CommandId == Guid.Empty ? Guid.NewGuid() : request?.CommandId ?? Guid.NewGuid();

        var result = await _mediator.Send(new ApproveTransferCommand
        {
            CommandId = commandId,
            CorrelationId = ResolveCorrelationId(),
            TransferId = id,
            ApprovedBy = request?.ApprovedBy ?? string.Empty
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        var transfer = await _dbContext.Transfers
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (transfer is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, "Transfer not found."));
        }

        return Ok(ToResponse(transfer));
    }

    [HttpPost("{id:guid}/execute")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> ExecuteAsync(
        Guid id,
        [FromBody] ExecuteTransferRequest? request,
        CancellationToken cancellationToken = default)
    {
        var commandId = request?.CommandId == Guid.Empty ? Guid.NewGuid() : request?.CommandId ?? Guid.NewGuid();

        var result = await _mediator.Send(new ExecuteTransferCommand
        {
            CommandId = commandId,
            CorrelationId = ResolveCorrelationId(),
            TransferId = id
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        var transfer = await _dbContext.Transfers
            .AsNoTracking()
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (transfer is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, "Transfer not found."));
        }

        return Ok(ToResponse(transfer));
    }

    private static TransferResponse ToResponse(LKvitai.MES.Modules.Warehouse.Domain.Entities.Transfer transfer)
    {
        return new TransferResponse(
            transfer.Id,
            transfer.TransferNumber,
            transfer.FromWarehouse,
            transfer.ToWarehouse,
            ToApiStatus(transfer.Status),
            transfer.RequestedBy,
            transfer.ApprovedBy,
            transfer.ExecutedBy,
            transfer.RequestedAt,
            transfer.ApprovedAt,
            transfer.ExecutedAt,
            transfer.CompletedAt,
            transfer.Lines.Select(x => new TransferLineResponse(
                x.ItemId,
                x.Qty,
                x.FromLocationId,
                x.ToLocationId,
                x.LotId)).ToList());
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

    public sealed record TransferLineRequest(int ItemId, decimal Qty, int FromLocationId, int ToLocationId, Guid? LotId = null);

    public sealed record CreateTransferRequest(
        Guid CommandId,
        string FromWarehouse,
        string ToWarehouse,
        IReadOnlyList<TransferLineRequest> Lines,
        string? RequestedBy = null);

    public sealed record SubmitTransferRequest(Guid CommandId);

    public sealed record ApproveTransferRequest(Guid CommandId, string? ApprovedBy = null);

    public sealed record ExecuteTransferRequest(Guid CommandId);

    public sealed record TransferLineResponse(int ItemId, decimal Qty, int FromLocationId, int ToLocationId, Guid? LotId);

    public sealed record TransferResponse(
        Guid Id,
        string TransferNumber,
        string FromWarehouse,
        string ToWarehouse,
        string Status,
        string RequestedBy,
        string? ApprovedBy,
        string? ExecutedBy,
        DateTimeOffset RequestedAt,
        DateTimeOffset? ApprovedAt,
        DateTimeOffset? ExecutedAt,
        DateTimeOffset? CompletedAt,
        IReadOnlyList<TransferLineResponse> Lines);

    public sealed record PagedResponse<T>(
        IReadOnlyList<T> Items,
        int TotalCount,
        int PageNumber,
        int PageSize);

    private static string ToApiStatus(LKvitai.MES.Modules.Warehouse.Domain.Entities.TransferStatus status)
    {
        return status switch
        {
            LKvitai.MES.Modules.Warehouse.Domain.Entities.TransferStatus.PendingApproval => "PENDING_APPROVAL",
            LKvitai.MES.Modules.Warehouse.Domain.Entities.TransferStatus.InTransit => "IN_TRANSIT",
            _ => status.ToString().ToUpperInvariant()
        };
    }
}
