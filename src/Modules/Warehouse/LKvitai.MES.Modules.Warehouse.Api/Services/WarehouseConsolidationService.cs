using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Api.Configuration;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Application.Projections;
using LKvitai.MES.Modules.Warehouse.Domain;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using IDocumentStore = Marten.IDocumentStore;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

/// <summary>One non-zero (warehouse, location, SKU) balance that sits under a warehouse code other than the configured one.</summary>
public sealed record ConsolidationRow(string Warehouse, string Location, string Sku, decimal Quantity, string Status, string? Reason);

public sealed record ConsolidationPreview(string TargetWarehouse, IReadOnlyList<ConsolidationRow> Rows);

public sealed record ConsolidationResult(string TargetWarehouse, IReadOnlyList<ConsolidationRow> Moved, int SkippedRows);

public interface IWarehouseConsolidationService
{
    Task<ConsolidationPreview> PreviewAsync(CancellationToken ct = default);
    Task<ConsolidationResult> ApplyAsync(string reason, ProjectionAuditActor actor, CancellationToken ct = default);
}

/// <summary>
/// Moves physical stock that was recorded under an old warehouse code to the configured code.
/// The quantity is computed from committed events (never from async projections). Per row, two
/// StockMoved events are written in one transaction: old code <c>cell → WAREHOUSE_MERGE</c>, then
/// configured code <c>WAREHOUSE_MERGE → cell</c>. Rows with a reservation or HARD lock, negative
/// balances and virtual locations are shown but never moved.
/// </summary>
public sealed class WarehouseConsolidationService : IWarehouseConsolidationService
{
    public const string MergeLocation = PhysicalLocation.WarehouseMergeCode;
    public const string Movable = "Movable";
    public const string Blocked = "Blocked";
    public const string Virtual = "Virtual";

    private readonly WarehouseDbContext _db;
    private readonly IDocumentStore _store;
    private readonly IBalanceGuardLockFactory _locks;
    private readonly IWarehouseCodeProvider _warehouse;

    public WarehouseConsolidationService(
        WarehouseDbContext db, IDocumentStore store, IBalanceGuardLockFactory locks, IWarehouseCodeProvider warehouse)
    {
        _db = db;
        _store = store;
        _locks = locks;
        _warehouse = warehouse;
    }

    public async Task<ConsolidationPreview> PreviewAsync(CancellationToken ct = default)
        => new(_warehouse.Code, await ComputeAsync(ct));

    public async Task<ConsolidationResult> ApplyAsync(string reason, ProjectionAuditActor actor, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        var code = _warehouse.Code;
        var candidates = (await ComputeAsync(ct)).Where(x => x.Status == Movable).ToList();
        if (candidates.Count == 0) return new(code, [], 0);

        await using var guard = await _locks.CreateAsync(ct);
        await guard.AcquireAsync(candidates.SelectMany(LockKeys).Distinct().Order().ToArray(), ct);

        // Reservations, locks and balances may have changed while waiting for the lock.
        var wanted = candidates.Select(Key).ToHashSet();
        var fresh = (await ComputeAsync(ct)).Where(x => x.Status == Movable && wanted.Contains(Key(x))).ToList();
        var skipped = candidates.Count - fresh.Count;
        if (fresh.Count == 0) return new(code, [], skipped);

        var operatorId = Guid.TryParse(actor.UserId, out var parsed) ? parsed : Guid.Empty;
        var writes = new List<(string Stream, StockMovedEvent Event)>();
        foreach (var row in fresh)
        {
            var reasonText = $"Warehouse consolidation {row.Warehouse} -> {code}: {reason.Trim()}";
            writes.Add((StockLedgerStreamId.For(row.Warehouse, row.Location, row.Sku), new StockMovedEvent
            {
                MovementId = DeterministicId("out", row), SKU = row.Sku, Quantity = row.Quantity,
                FromLocation = row.Location, ToLocation = MergeLocation, MovementType = MovementType.Transfer,
                OperatorId = operatorId, Reason = reasonText
            }));
            writes.Add((StockLedgerStreamId.For(code, MergeLocation, row.Sku), new StockMovedEvent
            {
                MovementId = DeterministicId("in", row), SKU = row.Sku, Quantity = row.Quantity,
                FromLocation = MergeLocation, ToLocation = row.Location, MovementType = MovementType.Transfer,
                OperatorId = operatorId, Reason = reasonText
            }));
        }

        await _db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            await using var session = _store.LightweightSession(Marten.Services.SessionOptions.ForTransaction(
                (Npgsql.NpgsqlTransaction)transaction.GetDbTransaction(), false));
            foreach (var group in writes.GroupBy(x => x.Stream))
            {
                var state = await session.Events.FetchStreamStateAsync(group.Key, ct);
                var events = group.Select(x => (object)x.Event).ToArray();
                session.Events.Append(group.Key, (state?.Version ?? 0) + events.Length, events);
            }
            _db.SecurityAuditLogs.Add(new SecurityAuditLog
            {
                UserId = actor.UserId, Action = "WarehouseConsolidationApplied", Resource = "Warehouse", ResourceId = code,
                IpAddress = actor.IpAddress,
                UserAgent = actor.UserAgent.Length > 1000 ? actor.UserAgent[..1000] : actor.UserAgent,
                Timestamp = DateTimeOffset.UtcNow,
                Details = JsonSerializer.Serialize(new
                {
                    reason = reason.Trim(), targetWarehouse = code, rowCount = fresh.Count, skippedRows = skipped,
                    rows = fresh.Select(x => new { x.Warehouse, x.Location, x.Sku, x.Quantity })
                })
            });
            await _db.SaveChangesAsync(acceptAllChangesOnSuccess: false, ct);
            // Both legs of every row and the audit record commit or roll back together.
            await session.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            _db.ChangeTracker.AcceptAllChanges();
        });
        await guard.CommitAsync(ct);
        return new(code, fresh, skipped);
    }

    private IEnumerable<long> LockKeys(ConsolidationRow row)
    {
        yield return StockLockKey.ForLocation(row.Warehouse, row.Location, row.Sku);
        yield return StockLockKey.ForLocation(_warehouse.Code, row.Location, row.Sku);
        yield return StockLockKey.ForLocation(_warehouse.Code, MergeLocation, row.Sku);
    }

    private async Task<List<ConsolidationRow>> ComputeAsync(CancellationToken ct)
    {
        var code = _warehouse.Code;
        await using var session = _store.QuerySession();
        var balances = (await CommittedLocationStock.ReadBalancesAsync(session, null, ct))
            .Where(x => x.Quantity != 0m && !string.Equals(x.Warehouse, code, StringComparison.OrdinalIgnoreCase)
                        // The transit cell is this tool's own bookkeeping; its two legs net to zero.
                        && !string.Equals(x.Location, MergeLocation, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Warehouse, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Location, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Sku, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (balances.Count == 0) return [];

        var physicalCells = (await _db.Locations.AsNoTracking().Where(x => !x.IsVirtual).Select(x => x.Code).ToListAsync(ct))
            .Where(PhysicalLocation.IsPhysicalCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var blockers = await ReadBlockersAsync(session, ct);

        return balances.Select(b =>
        {
            if (!physicalCells.Contains(b.Location))
                return new ConsolidationRow(b.Warehouse, b.Location, b.Sku, b.Quantity, Virtual, null);
            if (b.Quantity < 0m)
                return new ConsolidationRow(b.Warehouse, b.Location, b.Sku, b.Quantity, Blocked, "NegativeBalance");
            return blockers.TryGetValue(Key(b.Warehouse, b.Location, b.Sku), out var why)
                ? new ConsolidationRow(b.Warehouse, b.Location, b.Sku, b.Quantity, Blocked, why)
                : new ConsolidationRow(b.Warehouse, b.Location, b.Sku, b.Quantity, Movable, null);
        }).ToList();
    }

    /// <summary>Cells that carry an active HARD lock or any reservation. HARD wins when both apply.</summary>
    private static async Task<Dictionary<string, string>> ReadBlockersAsync(Marten.IQuerySession session, CancellationToken ct)
    {
        var result = new Dictionary<string, string>();
        void Add(string warehouse, string location, string sku, string reason)
        {
            var key = Key(warehouse, location, sku);
            if (reason == "HardLock" || !result.ContainsKey(key)) result[key] = reason;
        }

        // Inline projection: authoritative for HARD locks.
        foreach (var l in await Marten.QueryableExtensions.ToListAsync(
                     session.Query<ActiveHardLockView>().Where(x => x.HardLockedQty > 0m), ct))
            Add(l.WarehouseId, l.Location, l.SKU, "HardLock");

        foreach (var s in await Marten.QueryableExtensions.ToListAsync(
                     session.Query<AvailableStockView>().Where(x =>
                         x.HardLockedQty > 0m || x.ReservedQty > 0m || x.SalesReservedQty > 0m), ct))
            Add(s.WarehouseId, s.Location, s.SKU, s.HardLockedQty > 0m ? "HardLock" : "Reservation");

        // Sales-order reservations from committed events: the async view may lag behind them.
        var reservationIds = (await CommittedEventData.ReadAsync<StockAllocatedEvent>(session, null, ct))
            .Where(x => x.SalesOrderReservation).Select(x => x.ReservationId).Distinct().ToList();
        foreach (var id in reservationIds)
            foreach (var a in await SalesReservationBalance.LoadAsync(session, id, ct))
                Add(a.WarehouseId, a.Location, a.SKU, "Reservation");
        return result;
    }

    private static string Key(ConsolidationRow row) => Key(row.Warehouse, row.Location, row.Sku);

    private static string Key(string warehouse, string location, string sku)
        => $"{warehouse.Trim().ToUpperInvariant()}|{location.Trim().ToUpperInvariant()}|{sku.Trim().ToUpperInvariant()}";

    /// <summary>Stable id from old code + cell + SKU + quantity, so a repeated application can never add a second movement.</summary>
    private static Guid DeterministicId(string leg, ConsolidationRow row)
    {
        var text = $"warehouse-merge:{leg}:{Key(row)}:{row.Quantity.ToString("0.#############################", System.Globalization.CultureInfo.InvariantCulture)}";
        return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(text)).AsSpan(0, 16));
    }
}
