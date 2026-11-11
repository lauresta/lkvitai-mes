using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Marten;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

internal static class WarehouseOperationPersistence
{
    // Warehouse EF state and Marten are configured with the same WarehouseDb connection.
    public static Task CommitAsync(WarehouseDbContext db, IDocumentStore store,
        string operationStream, object operationEvent, IReadOnlyList<StockMovedEvent> movements,
        CancellationToken cancellationToken, Action? afterEfSave = null) => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await using var session = store.LightweightSession(
            Marten.Services.SessionOptions.ForTransaction((NpgsqlTransaction)transaction.GetDbTransaction(), false));
        await db.SaveChangesAsync(acceptAllChangesOnSuccess: false, cancellationToken);
        afterEfSave?.Invoke();
        // A unique operation stream prevents concurrent requests from posting the same movement twice.
        session.Events.StartStream(operationStream, operationEvent);
        foreach (var movement in movements)
            session.Events.Append(StockLedgerStreamId.For("WH1", movement.FromLocation, movement.SKU), movement);
        await session.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.AcceptAllChanges();
    });
}
