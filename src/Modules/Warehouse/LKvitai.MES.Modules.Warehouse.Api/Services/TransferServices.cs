using System.Diagnostics;
using System.Diagnostics.Metrics;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Domain;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Marten;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using IDocumentStore = Marten.IDocumentStore;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public interface ITransferStockAvailabilityService
{
    Task<decimal> GetAvailableQtyAsync(
        string warehouseId,
        string locationCode,
        string sku,
        CancellationToken cancellationToken = default);
}

public sealed class MartenTransferStockAvailabilityService : ITransferStockAvailabilityService
{
    private readonly IDocumentStore _documentStore;

    public MartenTransferStockAvailabilityService(IDocumentStore documentStore)
    {
        _documentStore = documentStore;
    }

    public async Task<decimal> GetAvailableQtyAsync(
        string warehouseId,
        string locationCode,
        string sku,
        CancellationToken cancellationToken = default)
    {
        await using var querySession = _documentStore.QuerySession();

        var normalizedWarehouseId = warehouseId.Trim();
        var normalizedLocation = locationCode.Trim();
        var normalizedSku = sku.Trim();

        var rows = await Marten.QueryableExtensions.ToListAsync(
            querySession.Query<AvailableStockView>()
                .Where(x =>
                    x.WarehouseId == normalizedWarehouseId &&
                    x.Location == normalizedLocation &&
                    x.SKU == normalizedSku),
            cancellationToken);

        return rows.Sum(x => x.AvailableQty);
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
            x => distinctItemIds.Contains(x.Id),
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
            x => distinctLocationIds.Contains(x.Id),
            cancellationToken);
        if (locationCount != distinctLocationIds.Count)
        {
            return Result.Fail(DomainErrorCodes.NotFound, "One or more transfer line locations were not found.");
        }

        var transfer = new Transfer
        {
            TransferNumber = await GenerateTransferNumberAsync(cancellationToken),
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
                ToLocationId = line.ToLocationId,
                LotId = line.LotId
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

    private async Task<string> GenerateTransferNumberAsync(CancellationToken cancellationToken)
    {
        var utcNow = DateTimeOffset.UtcNow;
        var date = new DateTimeOffset(utcNow.UtcDateTime.Date, TimeSpan.Zero);
        var nextDate = date.AddDays(1);
        var existingCount = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(
            _dbContext.Transfers.AsNoTracking(),
            x => x.RequestedAt >= date && x.RequestedAt < nextDate,
            cancellationToken);

        return $"TRF-{utcNow:yyyyMMdd}-{existingCount + 1:000}";
    }
}

public sealed class SubmitTransferCommandHandler : IRequestHandler<SubmitTransferCommand, Result>
{
    private readonly WarehouseDbContext _dbContext;

    public SubmitTransferCommandHandler(WarehouseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> Handle(SubmitTransferCommand request, CancellationToken cancellationToken)
    {
        var transfer = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            _dbContext.Transfers,
            x => x.Id == request.TransferId,
            cancellationToken);
        if (transfer is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, "Transfer not found.");
        }

        var submitResult = transfer.Submit(request.CommandId, DateTimeOffset.UtcNow);
        if (!submitResult.IsSuccess)
        {
            return submitResult;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
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
    private const int StockLedgerAppendMaxRetries = 3;

    private readonly WarehouseDbContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly ITransferStockAvailabilityService _stockAvailabilityService;
    private readonly IStockLedgerRepository _stockLedgerRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ExecuteTransferCommandHandler> _logger;

    public ExecuteTransferCommandHandler(
        WarehouseDbContext dbContext,
        IEventBus eventBus,
        ITransferStockAvailabilityService stockAvailabilityService,
        IStockLedgerRepository stockLedgerRepository,
        ICurrentUserService currentUserService,
        ILogger<ExecuteTransferCommandHandler> logger)
    {
        _dbContext = dbContext;
        _eventBus = eventBus;
        _stockAvailabilityService = stockAvailabilityService;
        _stockLedgerRepository = stockLedgerRepository;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result> Handle(ExecuteTransferCommand request, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();

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
                    transfer.FromWarehouse,
                    line.FromLocation.Code,
                    line.Item.InternalSKU,
                    cancellationToken);

                if (available < line.Qty)
                {
                    _logger.LogWarning(
                        "Transfer execute blocked: insufficient available stock. TransferId={TransferId}, TransferNumber={TransferNumber}, FromWarehouse={FromWarehouse}, FromLocation={FromLocation}, ToWarehouse={ToWarehouse}, ToLocation={ToLocation}, ItemId={ItemId}, SKU={SKU}, RequestedQty={RequestedQty}, AvailableQty={AvailableQty}, Delta={MissingQty}",
                        transfer.Id,
                        transfer.TransferNumber,
                        transfer.FromWarehouse,
                        line.FromLocation.Code,
                        transfer.ToWarehouse,
                        line.ToLocation.Code,
                        line.Item.Id,
                        line.Item.InternalSKU,
                        line.Qty,
                        available,
                        line.Qty - available);

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

                var startResult = transfer.StartExecution(
                    _currentUserService.GetCurrentUserId(),
                    request.CommandId,
                    DateTimeOffset.UtcNow);
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

                    var toTransitMovement = new StockMovedEvent
                    {
                        MovementId = Guid.NewGuid(),
                        SKU = itemSku,
                        Quantity = line.Qty,
                        FromLocation = fromLocationCode,
                        ToLocation = inTransitLocation.Code,
                        MovementType = MovementType.Transfer,
                        OperatorId = operatorId,
                        Reason = $"Transfer {transfer.TransferNumber}"
                    };

                    var appendToTransitResult = await AppendStockMovementAsync(
                        transfer.FromWarehouse,
                        toTransitMovement.FromLocation,
                        toTransitMovement,
                        cancellationToken);
                    if (!appendToTransitResult.IsSuccess)
                    {
                        return appendToTransitResult;
                    }

                    await _eventBus.PublishAsync(toTransitMovement, cancellationToken);

                    var fromTransitMovement = new StockMovedEvent
                    {
                        MovementId = Guid.NewGuid(),
                        SKU = itemSku,
                        Quantity = line.Qty,
                        FromLocation = inTransitLocation.Code,
                        ToLocation = toLocationCode,
                        MovementType = MovementType.Transfer,
                        OperatorId = operatorId,
                        Reason = $"Transfer {transfer.TransferNumber}"
                    };

                    var appendFromTransitResult = await AppendStockMovementAsync(
                        transfer.ToWarehouse,
                        fromTransitMovement.FromLocation,
                        fromTransitMovement,
                        cancellationToken);
                    if (!appendFromTransitResult.IsSuccess)
                    {
                        return appendFromTransitResult;
                    }

                    await _eventBus.PublishAsync(fromTransitMovement, cancellationToken);
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
        });
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

    private async Task<Result> AppendStockMovementAsync(
        string warehouseId,
        string streamLocation,
        StockMovedEvent movementEvent,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(warehouseId))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Transfer warehouse is required.");
        }

        if (string.IsNullOrWhiteSpace(streamLocation))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Transfer location is required.");
        }

        var streamId = StockLedgerStreamId.For(
            warehouseId.Trim().ToUpperInvariant(),
            streamLocation.Trim(),
            movementEvent.SKU);

        for (var attempt = 1; attempt <= StockLedgerAppendMaxRetries; attempt++)
        {
            var (_, version) = await _stockLedgerRepository.LoadAsync(streamId, cancellationToken);

            try
            {
                await _stockLedgerRepository.AppendEventAsync(
                    streamId,
                    movementEvent,
                    version,
                    cancellationToken);

                return Result.Ok();
            }
            catch (ConcurrencyException ex) when (attempt < StockLedgerAppendMaxRetries)
            {
                _logger.LogWarning(
                    ex,
                    "Stock ledger concurrency conflict for stream {StreamId}, attempt {Attempt}/{MaxAttempts}",
                    streamId,
                    attempt,
                    StockLedgerAppendMaxRetries);

                await Task.Delay(50 * attempt, cancellationToken);
            }
            catch (ConcurrencyException ex)
            {
                _logger.LogError(
                    ex,
                    "Stock ledger append failed after retries for stream {StreamId}",
                    streamId);

                return Result.Fail(
                    DomainErrorCodes.ConcurrencyConflict,
                    $"Stock movement append failed for stream '{streamId}'.");
            }
        }

        return Result.Fail(
            DomainErrorCodes.ConcurrencyConflict,
            $"Stock movement append failed for stream '{streamId}'.");
    }

    private static Guid ResolveOperatorId(string userId)
    {
        return Guid.TryParse(userId, out var parsed) ? parsed : Guid.Empty;
    }
}
