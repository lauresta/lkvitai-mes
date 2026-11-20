using LKvitai.MES.Modules.Warehouse.Api.Configuration;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Marten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

internal static class WarehouseOperationPersistence
{
    // Warehouse EF state and Marten are configured with the same WarehouseDb connection.
    // Movements that cross the stock boundary also write StockValueChanged in the same transaction.
    public static async Task CommitAsync(WarehouseDbContext db, IDocumentStore store,
        string operationStream, object operationEvent, IReadOnlyList<StockMovedEvent> movements, string? warehouseCode,
        CancellationToken cancellationToken, Action? afterEfSave = null, Func<IDocumentSession, Task>? appendRelated = null)
    {
        IReadOnlyList<StockValueChanged> valueEvents = [];
        await StockValueRetry.RunAsync(() => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (movements.Count > 0 && string.IsNullOrWhiteSpace(warehouseCode))
                throw new InvalidOperationException("A warehouse code is required to write stock movements.");
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await using var session = store.LightweightSession(
                Marten.Services.SessionOptions.ForTransaction((NpgsqlTransaction)transaction.GetDbTransaction(), false));
            await db.SaveChangesAsync(acceptAllChangesOnSuccess: false, cancellationToken);
            afterEfSave?.Invoke();
            if (appendRelated is not null) await appendRelated(session);
            // A unique operation stream prevents concurrent requests from posting the same movement twice.
            session.Events.StartStream(operationStream, operationEvent);
            foreach (var movement in movements)
                session.Events.Append(StockLedgerStreamId.For(warehouseCode!, movement.FromLocation, movement.SKU), movement);
            valueEvents = await StockValueWriter.AppendAsync(session, db, movements.Select(m => StockValueRequest.ForMovement(
                m.SKU, m.Quantity, m.FromLocation, m.ToLocation,
                new StockValueDocument(operationEvent.GetType().Name, operationStream, null, m.Reason))), cancellationToken);
            await session.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            db.ChangeTracker.AcceptAllChanges();
        }), cancellationToken);
        await StockValueWriter.ProjectAsync(db, NullLogger.Instance, valueEvents, cancellationToken);
    }
}
