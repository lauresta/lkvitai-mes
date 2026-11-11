using LKvitai.MES.Modules.Warehouse.Api.Configuration;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Integration.Carrier;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using MediatR;
using IDocumentStore = Marten.IDocumentStore;
using Movement = LKvitai.MES.Modules.Warehouse.Domain.MovementType;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public sealed class DispatchShipmentCommandHandler : IRequestHandler<DispatchShipmentCommand, Result>
{
    private readonly WarehouseDbContext _dbContext;
    private readonly IDocumentStore _store;
    private readonly IWarehouseCodeProvider _warehouseCode;
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
        ILogger<DispatchShipmentCommandHandler> logger,
        IDocumentStore store,
        IWarehouseCodeProvider warehouseCode)
    {
        _dbContext = dbContext;
        _store = store;
        _warehouseCode = warehouseCode;
        _carrierApiService = carrierApiService;
        _eventBus = eventBus;
        _currentUserService = currentUserService;
        _businessTelemetryService = businessTelemetryService;
        _logger = logger;
    }

    public async Task<Result> Handle(DispatchShipmentCommand request, CancellationToken cancellationToken)
    {
        var shipment = await _dbContext.Shipments
            .Include(x => x.Lines).ThenInclude(x => x.Item)
            .FirstOrDefaultAsync(x => x.Id == request.ShipmentId, cancellationToken);

        if (shipment is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, "Shipment not found.");
        }

        var outboundOrder = await _dbContext.OutboundOrders
            .Include(x => x.SalesOrder)
                .ThenInclude(x => x!.Lines)
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == shipment.OutboundOrderId, cancellationToken);

        if (outboundOrder is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, "Outbound order for shipment was not found.");
        }

        var carrier = request.Carrier?.Trim().ToUpperInvariant() ?? string.Empty;
        var dispatchAt = request.DispatchTime ?? DateTimeOffset.UtcNow;

        if (string.IsNullOrWhiteSpace(request.Carrier) || string.IsNullOrWhiteSpace(request.ManualTrackingNumber))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Carrier and tracking number are required.");
        }
        var trackingNumber = request.ManualTrackingNumber.Trim();
        var manualTracking = true;

        if (!Enum.TryParse<Carrier>(carrier, true, out var parsedCarrier))
        {
            parsedCarrier = Carrier.Other;
        }

        if (!await _dbContext.Locations.AnyAsync(x => x.Code == "CUSTOMER" && x.IsVirtual, cancellationToken))
            return Result.Fail(DomainErrorCodes.ValidationError, "CUSTOMER dispatch location is missing.");
        if (shipment.Lines.Count == 0 || shipment.Lines.Any(x => x.Qty <= 0 || x.Item is null))
            return Result.Fail(DomainErrorCodes.ValidationError, "Shipment has no valid packed contents.");

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

        foreach (var line in outboundOrder.Lines)
        {
            line.ShippedQty = line.Qty;
        }

        if (outboundOrder.SalesOrder is { } salesOrder)
        {
            var salesOrderResult = salesOrder.Ship(dispatchAt);
            if (!salesOrderResult.IsSuccess)
            {
                return salesOrderResult;
            }

            foreach (var line in salesOrder.Lines)
            {
                line.ShippedQty = line.OrderedQty;
            }
        }

        var dispatched = new ShipmentDispatchedEvent
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
        };
        var movements = shipment.Lines.Select(line => new StockMovedEvent
        {
            MovementId = Guid.NewGuid(), SKU = line.Item!.InternalSKU, Quantity = line.Qty,
            FromLocation = "SHIPPING", ToLocation = "CUSTOMER", MovementType = Movement.Dispatch,
            OperatorId = Guid.TryParse(_currentUserService.GetCurrentUserId(), out var userId) ? userId : Guid.Empty,
            HandlingUnitId = line.HandlingUnitId, Reason = $"Dispatch {shipment.ShipmentNumber} / {trackingNumber}"
        }).ToList();
        await WarehouseOperationPersistence.CommitAsync(_dbContext, _store,
            $"shipment-dispatch:{shipment.Id:N}", dispatched, movements, _warehouseCode.Code, cancellationToken);
        foreach (var movement in movements) await _eventBus.PublishAsync(movement, cancellationToken);
        await _eventBus.PublishAsync(dispatched, cancellationToken);
        _businessTelemetryService.TrackShipmentDispatched(
            shipment.Id,
            outboundOrder.Id,
            carrier,
            dispatchAt,
            shipment.PackedAt.HasValue ? dispatchAt - shipment.PackedAt.Value : null);

        return Result.Ok();
    }
}
