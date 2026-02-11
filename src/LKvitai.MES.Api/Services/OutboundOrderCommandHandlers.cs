using LKvitai.MES.Application.Commands;
using LKvitai.MES.Application.Ports;
using LKvitai.MES.Application.Services;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Domain;
using LKvitai.MES.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HandlingUnitAggregate = LKvitai.MES.Domain.Aggregates.HandlingUnit;

namespace LKvitai.MES.Api.Services;

public sealed class PackOrderCommandHandler : IRequestHandler<PackOrderCommand, Result>
{
    private readonly WarehouseDbContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly ICurrentUserService _currentUserService;

    public PackOrderCommandHandler(
        WarehouseDbContext dbContext,
        IEventBus eventBus,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _eventBus = eventBus;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(PackOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.OutboundOrders
            .Include(x => x.Lines)
                .ThenInclude(x => x.Item)
            .FirstOrDefaultAsync(x => x.Id == request.OutboundOrderId, cancellationToken);

        if (order is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, "Outbound order not found.");
        }

        if (order.Status != OutboundOrderStatus.Picked)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Cannot pack order in status {order.Status.ToString().ToUpperInvariant()}, must be PICKED");
        }

        if (request.ScannedItems.Count == 0)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "At least one scanned item is required.");
        }

        var itemIds = order.Lines.Select(x => x.ItemId).Distinct().ToArray();
        var itemBarcodes = await _dbContext.Items
            .Where(x => itemIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                x.InternalSKU,
                x.PrimaryBarcode
            })
            .ToListAsync(cancellationToken);

        var barcodeLookup = itemBarcodes
            .Where(x => !string.IsNullOrWhiteSpace(x.PrimaryBarcode))
            .ToDictionary(x => x.PrimaryBarcode!, x => x, StringComparer.OrdinalIgnoreCase);

        var scannedByItem = new Dictionary<int, decimal>();
        foreach (var scanned in request.ScannedItems)
        {
            if (!barcodeLookup.TryGetValue(scanned.Barcode.Trim(), out var mapped))
            {
                return Result.Fail(
                    DomainErrorCodes.ValidationError,
                    $"Barcode {scanned.Barcode} does not match any order item");
            }

            scannedByItem[mapped.Id] = scannedByItem.GetValueOrDefault(mapped.Id) + scanned.Qty;
        }

        var missingSkus = order.Lines
            .Where(x => !scannedByItem.ContainsKey(x.ItemId))
            .Select(x => x.Item?.InternalSKU ?? x.ItemId.ToString())
            .ToList();

        if (missingSkus.Count > 0)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Missing items: {string.Join(", ", missingSkus)} not scanned");
        }

        foreach (var line in order.Lines)
        {
            var scannedQty = scannedByItem.GetValueOrDefault(line.ItemId);
            if (scannedQty != line.Qty)
            {
                var sku = line.Item?.InternalSKU ?? line.ItemId.ToString();
                return Result.Fail(
                    DomainErrorCodes.ValidationError,
                    $"Quantity mismatch for {sku}: expected {line.Qty}, scanned {scannedQty}");
            }
        }

        var huType = string.Equals(request.PackagingType, "PALLET", StringComparison.OrdinalIgnoreCase)
            ? LKvitai.MES.Domain.Aggregates.HandlingUnitType.PALLET
            : LKvitai.MES.Domain.Aggregates.HandlingUnitType.BOX;

        var now = DateTimeOffset.UtcNow;
        var handlingUnit = HandlingUnitAggregate.CreateShippingUnit(
            $"HU-{order.OrderNumber}-{Guid.NewGuid():N}".ToUpperInvariant(),
            huType,
            "SHIPPING");

        var shipment = new Shipment
        {
            OutboundOrderId = order.Id,
            ShippingHandlingUnitId = handlingUnit.HUId
        };

        var packShipmentResult = shipment.Pack(now);
        if (!packShipmentResult.IsSuccess)
        {
            return packShipmentResult;
        }

        foreach (var line in order.Lines)
        {
            line.ShippedQty = line.Qty;
            shipment.Lines.Add(new ShipmentLine
            {
                ItemId = line.ItemId,
                Qty = line.Qty,
                HandlingUnitId = handlingUnit.HUId
            });
        }

        var packOrderResult = order.Pack(shipment.Id, now);
        if (!packOrderResult.IsSuccess)
        {
            return packOrderResult;
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        _dbContext.HandlingUnits.Add(handlingUnit);
        _dbContext.Shipments.Add(shipment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var line in order.Lines)
        {
            await _eventBus.PublishAsync(new StockMovedEvent
            {
                MovementId = Guid.NewGuid(),
                SKU = line.Item?.InternalSKU ?? string.Empty,
                Quantity = line.Qty,
                FromLocation = "PICKING_STAGING",
                ToLocation = "SHIPPING",
                MovementType = MovementType.Dispatch,
                OperatorId = Guid.Empty,
                HandlingUnitId = handlingUnit.HUId
            }, cancellationToken);
        }

        await _eventBus.PublishAsync(new ShipmentPackedEvent
        {
            ShipmentId = shipment.Id,
            ShipmentNumber = shipment.ShipmentNumber,
            OutboundOrderId = order.Id,
            OutboundOrderNumber = order.OrderNumber,
            HandlingUnitId = handlingUnit.HUId,
            HandlingUnitCode = handlingUnit.LPN,
            PackagingType = request.PackagingType.ToUpperInvariant(),
            PackedAt = now.UtcDateTime,
            PackedBy = _currentUserService.GetCurrentUserId(),
            LabelPreviewUrl = $"/labels/preview?labelType=HU&lpn={Uri.EscapeDataString(handlingUnit.LPN)}",
            Lines = order.Lines.Select(x => new ShipmentLineSnapshot
            {
                ItemId = x.ItemId,
                ItemSku = x.Item?.InternalSKU ?? string.Empty,
                Qty = x.Qty
            }).ToList()
        }, cancellationToken);

        await tx.CommitAsync(cancellationToken);

        return Result.Ok();
    }
}
