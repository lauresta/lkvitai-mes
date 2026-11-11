using LKvitai.MES.Modules.Warehouse.Api.Configuration;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using IDocumentStore = Marten.IDocumentStore;
using Reservation = LKvitai.MES.Modules.Warehouse.Domain.Aggregates.Reservation;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public sealed class SalesOrderReservationService(WarehouseDbContext db, IDocumentStore store, IMediator mediator, IEventBus bus,
    IWarehouseCodeProvider warehouse)
{
    public async Task<Result> AllocateAsync(SalesOrder order, Func<Result> transition, CancellationToken ct)
    {
        if (order.Lines.Count == 0 || order.Lines.Any(x => x.Item is null || x.OrderedQty <= 0))
            return Result.Fail(DomainErrorCodes.ValidationError, "Order has no valid stock lines.");
        var result = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await using var session = store.LightweightSession(Marten.Services.SessionOptions.ForTransaction(
                (NpgsqlTransaction)transaction.GetDbTransaction(), false));
            var stream = Reservation.StreamIdFor(order.Id);
            if (await session.Events.FetchStreamStateAsync(stream, ct) is not null)
                return Result.Fail(DomainErrorCodes.ValidationError, "Order already has a reservation.");
            session.Events.StartStream(stream, new ReservationCreatedEvent
            {
                ReservationId = order.Id, Purpose = SalesReservationBalance.Purpose,
                RequestedLines = order.Lines.GroupBy(x => x.Item!.InternalSKU, StringComparer.OrdinalIgnoreCase)
                    .Select(x => new ReservationLineDto { SKU = x.Key, Quantity = x.Sum(y => y.OrderedQty) }).ToList()
            });
            await session.SaveChangesAsync(ct);
            var allocation = await mediator.Send(new AllocateReservationCommand
                { ReservationId = order.Id, WarehouseId = warehouse.Code }, ct);
            if (!allocation.IsSuccess) return allocation;
            var status = transition();
            if (!status.IsSuccess) return status;
            var assigned = order.AssignReservation(order.Id);
            if (!assigned.IsSuccess) return assigned;
            foreach (var line in order.Lines) line.AllocatedQty = line.OrderedQty;
            await db.SaveChangesAsync(acceptAllChangesOnSuccess: false, ct);
            await transaction.CommitAsync(ct);
            db.ChangeTracker.AcceptAllChanges();
            return Result.Ok();
        });
        if (!result.IsSuccess)
        {
            if (result.ErrorCode == DomainErrorCodes.InsufficientAvailableStock)
            {
                var pending = order.WaitForStock();
                if (!pending.IsSuccess) return pending;
                await db.SaveChangesAsync(ct);
                return Result.Ok();
            }
            return result;
        }
        await bus.PublishAsync(new SalesOrderAllocatedEvent
        {
            Id = order.Id, ReservationId = order.Id, AllocatedAt = order.AllocatedAt!.Value.UtcDateTime
        }, ct);
        return Result.Ok();
    }
}
