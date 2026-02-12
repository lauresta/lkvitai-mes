using System.Diagnostics;
using System.Diagnostics.Metrics;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Application.Commands;
using LKvitai.MES.Application.Ports;
using LKvitai.MES.Application.Services;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Domain;
using LKvitai.MES.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using Marten;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using IDocumentStore = Marten.IDocumentStore;

namespace LKvitai.MES.Api.Services;

public interface ITransferStockAvailabilityService
{
    Task<decimal> GetAvailableQtyAsync(string locationCode, string sku, CancellationToken cancellationToken = default);
}

public sealed class MartenTransferStockAvailabilityService : ITransferStockAvailabilityService
{
    private const string DefaultWarehouseId = "WH1";

    private readonly IDocumentStore _documentStore;

    public MartenTransferStockAvailabilityService(IDocumentStore documentStore)
    {
        _documentStore = documentStore;
    }

    public async Task<decimal> GetAvailableQtyAsync(
        string locationCode,
        string sku,
        CancellationToken cancellationToken = default)
    {
        await using var querySession = _documentStore.QuerySession();

        var query = querySession.Query<AvailableStockView>()
            .Where(x => x.WarehouseId == DefaultWarehouseId && x.Location == locationCode && x.SKU == sku);

        return await Marten.QueryableExtensions.SumAsync(query, x => x.AvailableQty, cancellationToken);
    }
}

public sealed class CreateTransferCommandHandler : IRequestHandler<CreateTransferCommand, Result>
{
    private static readonly Meter TransferMeter = new("LKvitai.MES.Transfers");
    private static readonly Counter<long> TransfersCreatedTotal =
        TransferMeter.CreateCounter<long>("transfers_created_total");

    private readonly WarehouseDbContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CreateTransferCommandHandler> _logger;

    public CreateTransferCommandHandler(
        WarehouseDbContext dbContext,
        IEventBus eventBus,
        ICurrentUserService currentUserService,
        ILogger<CreateTransferCommandHandler> logger)
    {
        _dbContext = dbContext;
        _eventBus = eventBus;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result> Handle(CreateTransferCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FromWarehouse))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "FromWarehouse is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ToWarehouse))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "ToWarehouse is required.");
        }

        if (string.Equals(request.FromWarehouse, request.ToWarehouse, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "FromWarehouse and ToWarehouse must be different.");
        }

        if (request.Lines.Count == 0)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "At least one transfer line is required.");
        }

        foreach (var line in request.Lines)
        {
            if (line.ItemId <= 0)
            {
                return Result.Fail(DomainErrorCodes.ValidationError, "Transfer line ItemId must be greater than 0.");
            }

            if (line.Qty <= 0m)
            {
                return Result.Fail(DomainErrorCodes.ValidationError, "Transfer line Qty must be greater than 0.");
            }

            if (line.FromLocationId <= 0 || line.ToLocationId <= 0)
            {
                return Result.Fail(
                    DomainErrorCodes.ValidationError,
                    "Transfer line FromLocationId and ToLocationId must be greater than 0.");
            }

            if (line.FromLocationId == line.ToLocationId)
            {
                return Result.Fail(DomainErrorCodes.ValidationError, "Transfer line locations must be different.");
            }
        }

        var distinctItemIds = request.Lines.Select(x => x.ItemId).Distinct().ToList();
        var itemCount = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(
            _dbContext.Items.AsNoTracking(),
            x => Enumerable.Contains(distinctItemIds, x.Id),
            cancellationToken);
        if (itemCount != distinctItemIds.Count)
        {
            return Result.Fail(DomainErrorCodes.NotFound, "One or more transfer line items were not found.");
        }

        var distinctLocationIds = request.Lines
            .SelectMany(x => new[] { x.FromLocationId, x.ToLocationId })
            .Distinct()
            .ToList();
        var locationCount = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(
            _dbContext.Locations.AsNoTracking(),
            x => Enumerable.Contains(distinctLocationIds, x.Id),
            cancellationToken);
        if (locationCount != distinctLocationIds.Count)
        {
            return Result.Fail(DomainErrorCodes.NotFound, "One or more transfer line locations were not found.");
        }

        var transfer = new Transfer
        {
            TransferNumber = $"TRF-{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            FromWarehouse = request.FromWarehouse.Trim().ToUpperInvariant(),
            ToWarehouse = request.ToWarehouse.Trim().ToUpperInvariant(),
            RequestedBy = string.IsNullOrWhiteSpace(request.RequestedBy)
                ? _currentUserService.GetCurrentUserId()
                : request.RequestedBy.Trim(),
            RequestedAt = DateTimeOffset.UtcNow,
            CreateCommandId = request.CommandId
        };

        foreach (var line in request.Lines)
        {
            transfer.Lines.Add(new TransferLine
            {
                ItemId = line.ItemId,
                Qty = line.Qty,
                FromLocationId = line.FromLocationId,
                ToLocationId = line.ToLocationId
            });
        }

        var stateResult = transfer.EnsureRequestedState();
        if (!stateResult.IsSuccess)
        {
            return stateResult;
        }

        _dbContext.Transfers.Add(transfer);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new TransferCreatedEvent
        {
            TransferId = transfer.Id,
            TransferNumber = transfer.TransferNumber,
            FromWarehouse = transfer.FromWarehouse,
            ToWarehouse = transfer.ToWarehouse,
            Status = transfer.Status.ToString().ToUpperInvariant(),
            RequestedBy = transfer.RequestedBy,
            RequestedAt = transfer.RequestedAt.UtcDateTime,
            Lines = transfer.Lines.Select(x => new TransferLineSnapshot
            {
                ItemId = x.ItemId,
                Qty = x.Qty,
                FromLocationId = x.FromLocationId,
                ToLocationId = x.ToLocationId
            }).ToList()
        }, cancellationToken);

        TransfersCreatedTotal.Add(1, new KeyValuePair<string, object?>("to_warehouse", transfer.ToWarehouse));
        _logger.LogInformation(
            "Transfer created: {TransferNumber}, From {FromWarehouse} To {ToWarehouse}",
            transfer.TransferNumber,
            transfer.FromWarehouse,
            transfer.ToWarehouse);

        return Result.Ok();
    }
}

public sealed class ApproveTransferCommandHandler : IRequestHandler<ApproveTransferCommand, Result>
{
    private readonly WarehouseDbContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ApproveTransferCommandHandler(
        WarehouseDbContext dbContext,
        IEventBus eventBus,
        ICurrentUserService currentUserService,
        IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _eventBus = eventBus;
        _currentUserService = currentUserService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result> Handle(ApproveTransferCommand request, CancellationToken cancellationToken)
    {
        var transfer = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            _dbContext.Transfers,
            x => x.Id == request.TransferId,
            cancellationToken);
        if (transfer is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, "Transfer not found.");
        }

        if (!transfer.RequiresApproval())
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Transfer approval is not required.");
        }

        var currentUser = _httpContextAccessor.HttpContext?.User;
        var canApprove = currentUser?.IsInRole(WarehouseRoles.WarehouseManager) == true ||
                         currentUser?.IsInRole(WarehouseRoles.WarehouseAdmin) == true;

        if (!canApprove)
        {
            return Result.Fail(DomainErrorCodes.Forbidden, "Manager approval is required for SCRAP transfers.");
        }

        var approvedBy = string.IsNullOrWhiteSpace(request.ApprovedBy)
            ? _currentUserService.GetCurrentUserId()
            : request.ApprovedBy.Trim();

        var approveResult = transfer.Approve(approvedBy, request.CommandId, DateTimeOffset.UtcNow);
        if (!approveResult.IsSuccess)
        {
            return approveResult;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new TransferApprovedEvent
        {
            TransferId = transfer.Id,
            TransferNumber = transfer.TransferNumber,
            ApprovedBy = approvedBy,
            ApprovedAt = transfer.ApprovedAt?.UtcDateTime ?? DateTime.UtcNow
        }, cancellationToken);

        return Result.Ok();
    }
}

public sealed class ExecuteTransferCommandHandler : IRequestHandler<ExecuteTransferCommand, Result>
{
    private static readonly Meter TransferMeter = new("LKvitai.MES.Transfers");
    private static readonly Counter<long> TransfersExecutedTotal =
        TransferMeter.CreateCounter<long>("transfers_executed_total");
    private static readonly Histogram<double> TransferExecutionDurationMs =
        TransferMeter.CreateHistogram<double>("transfer_execution_duration_ms");

    private readonly WarehouseDbContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly ITransferStockAvailabilityService _stockAvailabilityService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ExecuteTransferCommandHandler> _logger;

    public ExecuteTransferCommandHandler(
        WarehouseDbContext dbContext,
        IEventBus eventBus,
        ITransferStockAvailabilityService stockAvailabilityService,
        ICurrentUserService currentUserService,
        ILogger<ExecuteTransferCommandHandler> logger)
    {
        _dbContext = dbContext;
        _eventBus = eventBus;
        _stockAvailabilityService = stockAvailabilityService;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result> Handle(ExecuteTransferCommand request, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();

        var query = _dbContext.Transfers
            .Include(x => x.Lines)
                .ThenInclude(x => x.Item)
            .Include(x => x.Lines)
                .ThenInclude(x => x.FromLocation)
            .Include(x => x.Lines)
                .ThenInclude(x => x.ToLocation);
        var transfer = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            query,
            x => x.Id == request.TransferId,
            cancellationToken);
        if (transfer is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, "Transfer not found.");
        }

        if (transfer.Status == TransferStatus.PendingApproval)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Transfer requires approval before execution.");
        }

        if (transfer.Lines.Count == 0)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Transfer has no lines.");
        }

        foreach (var line in transfer.Lines)
        {
            if (line.Item is null || line.FromLocation is null || line.ToLocation is null)
            {
                return Result.Fail(DomainErrorCodes.NotFound, "Transfer line references missing item or location.");
            }

            var available = await _stockAvailabilityService.GetAvailableQtyAsync(
                line.FromLocation.Code,
                line.Item.InternalSKU,
                cancellationToken);

            if (available < line.Qty)
            {
                return Result.Fail(
                    DomainErrorCodes.InsufficientAvailableStock,
                    $"Insufficient stock at location {line.FromLocation.Code}");
            }
        }

        IDbContextTransaction? transaction = null;
        try
        {
            if (_dbContext.Database.IsRelational())
            {
                transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            }

            var startResult = transfer.StartExecution(request.CommandId, DateTimeOffset.UtcNow);
            if (!startResult.IsSuccess)
            {
                return startResult;
            }

            var inTransitLocation = await EnsureInTransitLocationAsync(transfer, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _eventBus.PublishAsync(new TransferExecutedEvent
            {
                TransferId = transfer.Id,
                TransferNumber = transfer.TransferNumber,
                InTransitLocationCode = inTransitLocation.Code,
                LineCount = transfer.Lines.Count,
                ExecutedAt = transfer.ExecutedAt?.UtcDateTime ?? DateTime.UtcNow
            }, cancellationToken);

            var operatorId = ResolveOperatorId(_currentUserService.GetCurrentUserId());
            foreach (var line in transfer.Lines)
            {
                var itemSku = line.Item!.InternalSKU;
                var fromLocationCode = line.FromLocation!.Code;
                var toLocationCode = line.ToLocation!.Code;

                await _eventBus.PublishAsync(new StockMovedEvent
                {
                    MovementId = Guid.NewGuid(),
                    SKU = itemSku,
                    Quantity = line.Qty,
                    FromLocation = fromLocationCode,
                    ToLocation = inTransitLocation.Code,
                    MovementType = MovementType.Transfer,
                    OperatorId = operatorId,
                    Reason = $"Transfer {transfer.TransferNumber}"
                }, cancellationToken);

                await _eventBus.PublishAsync(new StockMovedEvent
                {
                    MovementId = Guid.NewGuid(),
                    SKU = itemSku,
                    Quantity = line.Qty,
                    FromLocation = inTransitLocation.Code,
                    ToLocation = toLocationCode,
                    MovementType = MovementType.Transfer,
                    OperatorId = operatorId,
                    Reason = $"Transfer {transfer.TransferNumber}"
                }, cancellationToken);
            }

            var completeResult = transfer.Complete(DateTimeOffset.UtcNow);
            if (!completeResult.IsSuccess)
            {
                return completeResult;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await _eventBus.PublishAsync(new TransferCompletedEvent
            {
                TransferId = transfer.Id,
                TransferNumber = transfer.TransferNumber,
                CompletedAt = transfer.CompletedAt?.UtcDateTime ?? DateTime.UtcNow
            }, cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }

        TransfersExecutedTotal.Add(1, new KeyValuePair<string, object?>("to_warehouse", transfer.ToWarehouse));
        TransferExecutionDurationMs.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        _logger.LogInformation(
            "Transfer executed: {TransferNumber}, {LineCount} lines",
            transfer.TransferNumber,
            transfer.Lines.Count);

        return Result.Ok();
    }

    private async Task<Location> EnsureInTransitLocationAsync(Transfer transfer, CancellationToken cancellationToken)
    {
        var code = $"IN_TRANSIT_{transfer.TransferNumber}";
        var existing = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            _dbContext.Locations,
            x => x.Code == code,
            cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var location = new Location
        {
            Code = code,
            Barcode = code,
            Type = "Virtual",
            IsVirtual = true,
            Status = "Active",
            ZoneType = "General"
        };

        _dbContext.Locations.Add(location);
        return location;
    }

    private static Guid ResolveOperatorId(string userId)
    {
        return Guid.TryParse(userId, out var parsed) ? parsed : Guid.Empty;
    }
}
