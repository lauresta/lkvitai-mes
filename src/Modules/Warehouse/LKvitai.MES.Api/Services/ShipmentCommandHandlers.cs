using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Integration.Carrier;
using LKvitai.MES.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Api.Services;

public sealed class DispatchShipmentCommandHandler : IRequestHandler<DispatchShipmentCommand, Result>
{
    private static readonly TimeSpan[] RetryDelays =
    {
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4)
    };

    private readonly WarehouseDbContext _dbContext;
    private readonly ICarrierApiService _carrierApiService;
    private readonly IEventBus _eventBus;
    private readonly ICurrentUserService _currentUserService;
    private readonly IBusinessTelemetryService _businessTelemetryService;
    private readonly ILogger<DispatchShipmentCommandHandler> _logger;

    public DispatchShipmentCommandHandler(
        WarehouseDbContext dbContext,
        ICarrierApiService carrierApiService,
        IEventBus eventBus,
        ICurrentUserService currentUserService,
        IBusinessTelemetryService businessTelemetryService,
        ILogger<DispatchShipmentCommandHandler> logger)
    {
        _dbContext = dbContext;
        _carrierApiService = carrierApiService;
        _eventBus = eventBus;
        _currentUserService = currentUserService;
        _businessTelemetryService = businessTelemetryService;
        _logger = logger;
    }

    public async Task<Result> Handle(DispatchShipmentCommand request, CancellationToken cancellationToken)
    {
        var shipment = await _dbContext.Shipments
            .FirstOrDefaultAsync(x => x.Id == request.ShipmentId, cancellationToken);

        if (shipment is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, "Shipment not found.");
        }

        var outboundOrder = await _dbContext.OutboundOrders
            .FirstOrDefaultAsync(x => x.Id == shipment.OutboundOrderId, cancellationToken);

        if (outboundOrder is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, "Outbound order for shipment was not found.");
        }

        var carrier = request.Carrier.ToUpperInvariant();
        var dispatchAt = request.DispatchTime ?? DateTimeOffset.UtcNow;

        string? trackingNumber = null;
        var manualTracking = false;

        for (var attempt = 0; attempt < RetryDelays.Length; attempt++)
        {
            var trackingResult = await _carrierApiService.GenerateTrackingNumberAsync(
                shipment.Id,
                carrier,
                cancellationToken);

            if (trackingResult.IsSuccess)
            {
                trackingNumber = trackingResult.Value;
                break;
            }

            _logger.LogWarning(
                "Carrier API call failed (attempt {Attempt}/3): {Error}",
                attempt + 1,
                trackingResult.Error);

            if (attempt < RetryDelays.Length - 1)
            {
                await Task.Delay(RetryDelays[attempt], cancellationToken);
            }
        }

        if (string.IsNullOrWhiteSpace(trackingNumber))
        {
            if (string.IsNullOrWhiteSpace(request.ManualTrackingNumber))
            {
                return Result.Fail(
                    DomainErrorCodes.InternalError,
                    "Carrier API failed and no manual tracking number was provided.");
            }

            trackingNumber = request.ManualTrackingNumber.Trim();
            manualTracking = true;
        }

        if (!Enum.TryParse<Carrier>(carrier, true, out var parsedCarrier))
        {
            parsedCarrier = Carrier.Other;
        }

        var shipmentResult = shipment.Dispatch(parsedCarrier, trackingNumber, dispatchAt);
        if (!shipmentResult.IsSuccess)
        {
            return shipmentResult;
        }

        var orderResult = outboundOrder.Ship(dispatchAt);
        if (!orderResult.IsSuccess)
        {
            return orderResult;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new ShipmentDispatchedEvent
        {
            ShipmentId = shipment.Id,
            ShipmentNumber = shipment.ShipmentNumber,
            OutboundOrderId = outboundOrder.Id,
            OutboundOrderNumber = outboundOrder.OrderNumber,
            Carrier = carrier,
            TrackingNumber = trackingNumber,
            VehicleId = request.VehicleId,
            DispatchedAt = dispatchAt.UtcDateTime,
            DispatchedBy = _currentUserService.GetCurrentUserId(),
            ManualTracking = manualTracking
        }, cancellationToken);
        _businessTelemetryService.TrackShipmentDispatched(
            shipment.Id,
            outboundOrder.Id,
            carrier,
            dispatchAt,
            shipment.PackedAt.HasValue ? dispatchAt - shipment.PackedAt.Value : null);

        return Result.Ok();
    }
}
