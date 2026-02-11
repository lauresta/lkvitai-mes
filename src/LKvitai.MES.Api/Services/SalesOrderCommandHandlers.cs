using LKvitai.MES.Application.Commands;
using LKvitai.MES.Application.Ports;
using LKvitai.MES.Application.Services;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Api.Services;

public sealed class CreateSalesOrderCommandHandler : IRequestHandler<CreateSalesOrderCommand, Result>
{
    private readonly WarehouseDbContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CreateSalesOrderCommandHandler> _logger;

    public CreateSalesOrderCommandHandler(
        WarehouseDbContext dbContext,
        IEventBus eventBus,
        ICurrentUserService currentUserService,
        ILogger<CreateSalesOrderCommandHandler> logger)
    {
        _dbContext = dbContext;
        _eventBus = eventBus;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Result> Handle(CreateSalesOrderCommand request, CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .FirstOrDefaultAsync(x => x.Id == request.CustomerId, cancellationToken);

        if (customer is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, "Customer not found.");
        }

        if (customer.Status != CustomerStatus.Active)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Customer is not active.");
        }

        if (request.Lines.Count == 0)
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "At least one order line is required.");
        }

        var itemIds = request.Lines.Select(x => x.ItemId).Distinct().ToArray();
        var existingItemIds = await _dbContext.Items
            .Where(x => itemIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var missingItem = itemIds.FirstOrDefault(id => !existingItemIds.Contains(id));
        if (missingItem != 0)
        {
            return Result.Fail(DomainErrorCodes.NotFound, $"Item '{missingItem}' not found.");
        }

        var order = new SalesOrder
        {
            Id = request.SalesOrderId,
            CustomerId = request.CustomerId,
            ShippingAddress = request.ShippingAddress ?? customer.DefaultShippingAddress ?? customer.BillingAddress,
            RequestedDeliveryDate = request.RequestedDeliveryDate.HasValue
                ? DateOnly.FromDateTime(request.RequestedDeliveryDate.Value.Date)
                : null
        };

        foreach (var line in request.Lines)
        {
            if (line.Qty <= 0m)
            {
                return Result.Fail(DomainErrorCodes.ValidationError, "Line quantity must be greater than 0.");
            }

            order.Lines.Add(new SalesOrderLine
            {
                ItemId = line.ItemId,
                OrderedQty = line.Qty,
                UnitPrice = line.UnitPrice
            });
        }

        order.RecalculateTotals();

        _dbContext.SalesOrders.Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new SalesOrderCreatedEvent
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            CustomerId = order.CustomerId,
            OrderDate = order.OrderDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            RequestedDeliveryDate = order.RequestedDeliveryDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            Lines = order.Lines.Select(x => new SalesOrderLineSnapshot
            {
                ItemId = x.ItemId,
                Qty = x.OrderedQty,
                UnitPrice = x.UnitPrice
            }).ToList()
        }, cancellationToken);

        _logger.LogInformation(
            "SalesOrder {OrderNumber} created by {UserId}, total {TotalAmount}",
            order.OrderNumber,
            _currentUserService.GetCurrentUserId(),
            order.TotalAmount);

        return Result.Ok();
    }
}

public sealed class SubmitSalesOrderCommandHandler : IRequestHandler<SubmitSalesOrderCommand, Result>
{
    private readonly WarehouseDbContext _dbContext;
    private readonly IEventBus _eventBus;

    public SubmitSalesOrderCommandHandler(WarehouseDbContext dbContext, IEventBus eventBus)
    {
        _dbContext = dbContext;
        _eventBus = eventBus;
    }

    public async Task<Result> Handle(SubmitSalesOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.SalesOrders
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == request.SalesOrderId, cancellationToken);

        if (order is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, "Sales order not found.");
        }

        var requiresApproval = order.Customer?.CreditLimit.HasValue == true &&
                               order.TotalAmount > order.Customer.CreditLimit.Value;

        var submitResult = order.Submit(requiresApproval);
        if (!submitResult.IsSuccess)
        {
            return submitResult;
        }

        if (order.Status == SalesOrderStatus.Allocated)
        {
            var reservationResult = order.AssignReservation(Guid.NewGuid());
            if (!reservationResult.IsSuccess)
            {
                return reservationResult;
            }

            await _eventBus.PublishAsync(new SalesOrderAllocatedEvent
            {
                Id = order.Id,
                ReservationId = order.ReservationId!.Value,
                AllocatedAt = order.AllocatedAt?.UtcDateTime ?? DateTime.UtcNow
            }, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Ok();
    }
}

public sealed class ApproveSalesOrderCommandHandler : IRequestHandler<ApproveSalesOrderCommand, Result>
{
    private readonly WarehouseDbContext _dbContext;
    private readonly IEventBus _eventBus;

    public ApproveSalesOrderCommandHandler(WarehouseDbContext dbContext, IEventBus eventBus)
    {
        _dbContext = dbContext;
        _eventBus = eventBus;
    }

    public async Task<Result> Handle(ApproveSalesOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.SalesOrders
            .FirstOrDefaultAsync(x => x.Id == request.SalesOrderId, cancellationToken);

        if (order is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, "Sales order not found.");
        }

        var approveResult = order.Approve();
        if (!approveResult.IsSuccess)
        {
            return approveResult;
        }

        var reservationResult = order.AssignReservation(Guid.NewGuid());
        if (!reservationResult.IsSuccess)
        {
            return reservationResult;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new SalesOrderAllocatedEvent
        {
            Id = order.Id,
            ReservationId = order.ReservationId!.Value,
            AllocatedAt = order.AllocatedAt?.UtcDateTime ?? DateTime.UtcNow
        }, cancellationToken);

        return Result.Ok();
    }
}

public sealed class AllocateSalesOrderCommandHandler : IRequestHandler<AllocateSalesOrderCommand, Result>
{
    private readonly WarehouseDbContext _dbContext;
    private readonly IEventBus _eventBus;

    public AllocateSalesOrderCommandHandler(WarehouseDbContext dbContext, IEventBus eventBus)
    {
        _dbContext = dbContext;
        _eventBus = eventBus;
    }

    public async Task<Result> Handle(AllocateSalesOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.SalesOrders
            .FirstOrDefaultAsync(x => x.Id == request.SalesOrderId, cancellationToken);

        if (order is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, "Sales order not found.");
        }

        var allocationResult = order.Allocate(Guid.NewGuid());
        if (!allocationResult.IsSuccess)
        {
            return allocationResult;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new SalesOrderAllocatedEvent
        {
            Id = order.Id,
            ReservationId = order.ReservationId!.Value,
            AllocatedAt = order.AllocatedAt?.UtcDateTime ?? DateTime.UtcNow
        }, cancellationToken);

        return Result.Ok();
    }
}

public sealed class ReleaseSalesOrderCommandHandler : IRequestHandler<ReleaseSalesOrderCommand, Result>
{
    private readonly WarehouseDbContext _dbContext;
    private readonly IEventBus _eventBus;

    public ReleaseSalesOrderCommandHandler(WarehouseDbContext dbContext, IEventBus eventBus)
    {
        _dbContext = dbContext;
        _eventBus = eventBus;
    }

    public async Task<Result> Handle(ReleaseSalesOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.SalesOrders
            .Include(x => x.Customer)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Item)
            .FirstOrDefaultAsync(x => x.Id == request.SalesOrderId, cancellationToken);

        if (order is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, "Sales order not found.");
        }

        var releaseResult = order.Release();
        if (!releaseResult.IsSuccess)
        {
            return releaseResult;
        }

        OutboundOrder? outboundOrder = null;
        if (!order.OutboundOrderId.HasValue)
        {
            outboundOrder = new OutboundOrder
            {
                ReservationId = order.ReservationId ?? Guid.NewGuid(),
                Type = OutboundOrderType.Sales,
                SalesOrderId = order.Id
            };

            var markAllocatedResult = outboundOrder.MarkAllocated(outboundOrder.ReservationId);
            if (!markAllocatedResult.IsSuccess)
            {
                return markAllocatedResult;
            }

            var startPickingResult = outboundOrder.StartPicking();
            if (!startPickingResult.IsSuccess)
            {
                return startPickingResult;
            }

            foreach (var line in order.Lines)
            {
                outboundOrder.Lines.Add(new OutboundOrderLine
                {
                    ItemId = line.ItemId,
                    Qty = line.OrderedQty,
                    PickedQty = 0m,
                    ShippedQty = 0m
                });
            }

            _dbContext.OutboundOrders.Add(outboundOrder);
            var linkResult = order.LinkOutboundOrder(outboundOrder.Id);
            if (!linkResult.IsSuccess)
            {
                return linkResult;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (outboundOrder is not null)
        {
            await _eventBus.PublishAsync(new OutboundOrderCreatedEvent
            {
                Id = outboundOrder.Id,
                OrderNumber = outboundOrder.OrderNumber,
                Type = outboundOrder.Type.ToString().ToUpperInvariant(),
                Status = outboundOrder.Status.ToString().ToUpperInvariant(),
                CustomerName = order.Customer?.Name ?? string.Empty,
                OrderDate = outboundOrder.OrderDate.UtcDateTime,
                RequestedShipDate = outboundOrder.RequestedShipDate?.UtcDateTime,
                Lines = outboundOrder.Lines.Select(x => new ShipmentLineSnapshot
                {
                    ItemId = x.ItemId,
                    ItemSku = order.Lines.FirstOrDefault(l => l.ItemId == x.ItemId)?.Item?.InternalSKU ?? string.Empty,
                    Qty = x.Qty
                }).ToList()
            }, cancellationToken);
        }

        if (order.ReservationId.HasValue)
        {
            await _eventBus.PublishAsync(new SalesOrderReleasedEvent
            {
                Id = order.Id,
                ReservationId = order.ReservationId.Value,
                ReleasedAt = DateTime.UtcNow
            }, cancellationToken);
        }

        return Result.Ok();
    }
}

public sealed class CancelSalesOrderCommandHandler : IRequestHandler<CancelSalesOrderCommand, Result>
{
    private readonly WarehouseDbContext _dbContext;
    private readonly IEventBus _eventBus;
    private readonly ICurrentUserService _currentUserService;

    public CancelSalesOrderCommandHandler(
        WarehouseDbContext dbContext,
        IEventBus eventBus,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _eventBus = eventBus;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(CancelSalesOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.SalesOrders
            .FirstOrDefaultAsync(x => x.Id == request.SalesOrderId, cancellationToken);

        if (order is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, "Sales order not found.");
        }

        var cancelResult = order.Cancel(request.Reason);
        if (!cancelResult.IsSuccess)
        {
            return cancelResult;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new SalesOrderCancelledEvent
        {
            Id = order.Id,
            Reason = request.Reason,
            CancelledAt = DateTime.UtcNow,
            CancelledBy = _currentUserService.GetCurrentUserId()
        }, cancellationToken);

        return Result.Ok();
    }
}
