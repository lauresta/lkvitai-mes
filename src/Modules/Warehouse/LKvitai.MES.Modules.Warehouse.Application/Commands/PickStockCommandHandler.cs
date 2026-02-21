using LKvitai.MES.Modules.Warehouse.Application.Orchestration;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.Messages;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Modules.Warehouse.Application.Commands;

/// <summary>
/// Handles PickStockCommand.
///
/// [MITIGATION V-3] Transaction ordering:
///   1. Record StockMovement (StockLedger FIRST)
///   2. Try consume reservation synchronously
///   3. If consumption fails → publish to MassTransit saga for durable retry
///
/// The HU projection updates asynchronously — NOT waited on.
/// </summary>
public class PickStockCommandHandler : IRequestHandler<PickStockCommand, Result>
{
    private readonly IPickStockOrchestration _orchestration;
    private readonly IEventBus _eventBus;
    private readonly ILogger<PickStockCommandHandler> _logger;

    public PickStockCommandHandler(
        IPickStockOrchestration orchestration,
        IEventBus eventBus,
        ILogger<PickStockCommandHandler> logger)
    {
        _orchestration = orchestration;
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task<Result> Handle(PickStockCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "PickStock command received: Reservation {ReservationId}, SKU {SKU}, Qty {Quantity}, From {FromLocation}",
            request.ReservationId, request.SKU, request.Quantity, request.FromLocation);

        var result = await _orchestration.ExecuteAsync(
            request.ReservationId,
            request.HandlingUnitId,
            request.WarehouseId,
            request.SKU,
            request.Quantity,
            request.FromLocation,
            request.OperatorId,
            cancellationToken);

        if (result.IsSuccess)
        {
            _logger.LogInformation(
                "PickStock completed successfully: Reservation {ReservationId}, Movement {MovementId}",
                request.ReservationId, result.MovementId);
            return Result.Ok();
        }

        if (result.MovementCommitted)
        {
            // Movement is committed but consumption failed — defer to saga for durable retry.
            // This is NOT a caller-facing failure (the movement succeeded).
            _logger.LogWarning(
                "PickStock movement committed but consumption deferred to saga: " +
                "Reservation {ReservationId}, Movement {MovementId}, Error: {Error}",
                request.ReservationId, result.MovementId, result.Error);

            await _eventBus.PublishAsync(new ConsumePickReservationDeferred
            {
                CorrelationId = request.CorrelationId != Guid.Empty
                    ? request.CorrelationId
                    : Guid.NewGuid(),
                ReservationId = request.ReservationId,
                Quantity = request.Quantity,
                MovementId = result.MovementId,
                WarehouseId = request.WarehouseId,
                FromLocation = request.FromLocation,
                SKU = request.SKU,
                ReleasedHardLockLines = new List<HardLockLineDto>
                {
                    new()
                    {
                        WarehouseId = request.WarehouseId,
                        Location = request.FromLocation,
                        SKU = request.SKU,
                        HardLockedQty = request.Quantity
                    }
                }
            }, cancellationToken);

            // Return OK to caller — the movement is committed, consumption will complete eventually.
            return Result.Ok();
        }

        // Movement itself failed — this is a real failure.
        _logger.LogError(
            "PickStock failed: Reservation {ReservationId}, Error: {Error}",
            request.ReservationId, result.Error);

        return Result.Fail(result.Error ?? DomainErrorCodes.PickStockMovementFailed);
    }
}
