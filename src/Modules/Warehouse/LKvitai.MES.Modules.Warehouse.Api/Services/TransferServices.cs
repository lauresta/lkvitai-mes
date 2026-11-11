using System.Diagnostics;
using System.Diagnostics.Metrics;
using LKvitai.MES.Modules.Warehouse.Api.Configuration;
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
    private readonly IWarehouseCodeProvider _warehouseCode;

    public CreateTransferCommandHandler(
        WarehouseDbContext dbContext,
        IEventBus eventBus,
        ICurrentUserService currentUserService,
        ILogger<CreateTransferCommandHandler> logger,
        IWarehouseCodeProvider warehouseCode)
    {
        _warehouseCode = warehouseCode;
        _dbContext = dbContext;
        _eventBus = eventBus;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result> Handle(CreateTransferCommand request, CancellationToken cancellationToken)
    {
        // There is one warehouse: both ends are always the configured code. A caller that still
        // names a warehouse may only name that one; locations are what must differ (checked per line).
        var warehouse = _warehouseCode.Code;
        foreach (var named in new[] { request.FromWarehouse, request.ToWarehouse })
        {
            if (!string.IsNullOrWhiteSpace(named) &&
                !string.Equals(named.Trim(), warehouse, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Fail(DomainErrorCodes.ValidationError,
                    $"Transfers are only possible within warehouse '{warehouse}'.");
            }
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
            FromWarehouse = warehouse,
            ToWarehouse = warehouse,
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

    private readonly WarehouseDbContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly ITransferStockAvailabilityService _stockAvailabilityService;
    private readonly IDocumentStore _documentStore;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ExecuteTransferCommandHandler> _logger;
    private readonly IWarehouseCodeProvider _warehouseCode;

    public ExecuteTransferCommandHandler(
        WarehouseDbContext dbContext,
        IEventBus eventBus,
        ITransferStockAvailabilityService stockAvailabilityService,
        IDocumentStore documentStore,
        ICurrentUserService currentUserService,
        ILogger<ExecuteTransferCommandHandler> logger,
        IWarehouseCodeProvider warehouseCode)
    {
        _warehouseCode = warehouseCode;
        _dbContext = dbContext;
        _eventBus = eventBus;
        _stockAvailabilityService = stockAvailabilityService;
        _documentStore = documentStore;
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
                    _warehouseCode.Code,
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

            var startResult = transfer.StartExecution(_currentUserService.GetCurrentUserId(), request.CommandId, DateTimeOffset.UtcNow);
            if (!startResult.IsSuccess) return startResult;

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            await using var session = _documentStore.LightweightSession(Marten.Services.SessionOptions.ForTransaction(
                (Npgsql.NpgsqlTransaction)transaction.GetDbTransaction(), false));
            var inTransitLocation = await EnsureInTransitLocationAsync(transfer, cancellationToken);
            var executed = new TransferExecutedEvent
            {
                TransferId = transfer.Id, TransferNumber = transfer.TransferNumber,
                InTransitLocationCode = inTransitLocation.Code, LineCount = transfer.Lines.Count,
                ExecutedAt = transfer.ExecutedAt.GetValueOrDefault().UtcDateTime
            };
            var movements = BuildMovements(transfer, inTransitLocation.Code, _warehouseCode.Code,
                ResolveOperatorId(_currentUserService.GetCurrentUserId()));
            var completeResult = transfer.Complete(DateTimeOffset.UtcNow);
            if (!completeResult.IsSuccess) return completeResult;
            var completed = new TransferCompletedEvent
            {
                TransferId = transfer.Id, TransferNumber = transfer.TransferNumber,
                CompletedAt = transfer.CompletedAt.GetValueOrDefault().UtcDateTime
            };
            // One operation stream also rejects simultaneous executions of the same transfer.
            session.Events.StartStream($"transfer-execution:{transfer.Id:N}", executed, completed);
            foreach (var group in movements.GroupBy(x => StockLedgerStreamId.For(
                         x.Warehouse, x.Event.FromLocation, x.Event.SKU)))
            {
                var state = await session.Events.FetchStreamStateAsync(group.Key, cancellationToken);
                var events = group.Select(x => (object)x.Event).ToArray();
                session.Events.Append(group.Key, (state?.Version ?? 0) + events.Length, events);
            }
            await _dbContext.SaveChangesAsync(acceptAllChangesOnSuccess: false, cancellationToken);
            // Both legs of every line, together with EF state, commit or roll back together.
            await session.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _dbContext.ChangeTracker.AcceptAllChanges();

            // OnHandValue consumes the bus; publish only after the complete movement is committed.
            await _eventBus.PublishAsync(executed, cancellationToken);
            foreach (var movement in movements) await _eventBus.PublishAsync(movement.Event, cancellationToken);
            await _eventBus.PublishAsync(completed, cancellationToken);

            TransfersExecutedTotal.Add(1, new KeyValuePair<string, object?>("to_warehouse", transfer.ToWarehouse));
            TransferExecutionDurationMs.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            _logger.LogInformation(
                "Transfer executed: {TransferNumber}, {LineCount} lines",
                transfer.TransferNumber,
                transfer.Lines.Count);

            return Result.Ok();
        });
    }

    // Both legs are written under the one warehouse code, so the in-transit location nets to zero.
    private static (string Warehouse, StockMovedEvent Event)[] BuildMovements(Transfer transfer, string transit, string warehouse, Guid operatorId)
        => transfer.Lines.SelectMany(line => new[]
        {
            (warehouse, new StockMovedEvent
            {
                MovementId = Guid.NewGuid(), SKU = line.Item!.InternalSKU, Quantity = line.Qty,
                FromLocation = line.FromLocation!.Code, ToLocation = transit,
                MovementType = MovementType.Transfer, OperatorId = operatorId,
                Reason = $"Transfer {transfer.TransferNumber}"
            }),
            (warehouse, new StockMovedEvent
            {
                MovementId = Guid.NewGuid(), SKU = line.Item!.InternalSKU, Quantity = line.Qty,
                FromLocation = transit, ToLocation = line.ToLocation!.Code,
                MovementType = MovementType.Transfer, OperatorId = operatorId,
                Reason = $"Transfer {transfer.TransferNumber}"
            })
        }).ToArray();

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
