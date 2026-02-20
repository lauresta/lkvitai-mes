using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Orchestration;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.SharedKernel;
using Marten;
using Marten.Exceptions;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Infrastructure.Persistence;

/// <summary>
/// Marten implementation of <see cref="IReceiveGoodsOrchestration"/>.
///
/// Production-safe guarantees:
///   - Expected-version append on every stream (V-2 concurrency protection)
///   - Bounded retry (max 2 attempts) on <see cref="EventStreamUnexpectedMaxEventIdException"/>
///   - Defensive sealed-HU guard (verifies HU stream is new)
///   - Atomic multi-stream commit (single SaveChangesAsync)
///   - No raw exception messages in Result (stable domain error codes)
///
/// Event ordering: HU Created → StockMoved (per line) → HU Sealed.
/// Transaction boundary: all-or-nothing via single SaveChangesAsync.
/// </summary>
public class MartenReceiveGoodsOrchestration : IReceiveGoodsOrchestration
{
    private readonly IDocumentStore _store;
    private readonly ILogger<MartenReceiveGoodsOrchestration> _logger;

    private const string HuStreamPrefix = "handling-unit";
    private const int MaxAttempts = 2;

    public MartenReceiveGoodsOrchestration(
        IDocumentStore store,
        ILogger<MartenReceiveGoodsOrchestration> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<Result<Guid>> ExecuteAsync(ReceiveGoodsCommand command, CancellationToken ct)
    {
        var huId = Guid.NewGuid();
        var lpn = GenerateLPN();
        var huStreamId = $"{HuStreamPrefix}:{huId}";
        var now = DateTime.UtcNow;

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            await using var session = _store.LightweightSession();

            try
            {
                // ── Sealed guard: verify HU stream does not already exist ────
                var existingHuState = await session.Events.FetchStreamStateAsync(huStreamId, ct);
                if (existingHuState != null)
                {
                    _logger.LogWarning(
                        "HU stream {StreamId} already exists during ReceiveGoods — sealed guard triggered",
                        huStreamId);
                    return Result<Guid>.Fail(DomainErrorCodes.HandlingUnitSealed);
                }

                // ── Fetch stock-ledger stream versions for expected-version append ──
                var stockStreamVersions = new Dictionary<string, long>();
                foreach (var line in command.Lines)
                {
                    var stockStreamId = StockLedgerStreamId.For(
                        command.WarehouseId, command.Location, line.SKU);

                    if (!stockStreamVersions.ContainsKey(stockStreamId))
                    {
                        var state = await session.Events.FetchStreamStateAsync(stockStreamId, ct);
                        // Marten uses -2 for new streams (V-2 versioning scheme)
                        stockStreamVersions[stockStreamId] = state?.Version ?? -2;
                    }
                }

                // ── Step 1: Emit HandlingUnitCreated (new stream, expect version 0) ──
                var createdEvent = new HandlingUnitCreatedEvent
                {
                    HuId = huId,
                    LPN = lpn,
                    Type = command.HuType,
                    WarehouseId = command.WarehouseId,
                    Location = command.Location,
                    OperatorId = command.OperatorId,
                    Timestamp = now
                };

                long huExpectedVersion = 0;
                session.Events.Append(huStreamId, huExpectedVersion, createdEvent);

                _logger.LogDebug(
                    "Queued HandlingUnitCreated: HU {HuId} / LPN {LPN} at {Location}",
                    huId, lpn, command.Location);

                // ── Step 2: Record StockMovement per line (with expected-version) ──
                // Group by stream to handle multiple lines for same SKU
                var stockEventsByStream = new Dictionary<string, List<object>>();

                foreach (var line in command.Lines)
                {
                    var ledger = new StockLedger();
                    var stockMovedEvent = ledger.RecordMovement(
                        movementId: Guid.NewGuid(),
                        sku: line.SKU,
                        quantity: line.Quantity,
                        fromLocation: "SUPPLIER",
                        toLocation: command.Location,
                        movementType: MovementType.Receipt,
                        operatorId: command.OperatorId,
                        handlingUnitId: huId,
                        reason: $"Goods receipt to HU {lpn}");

                    var stockStreamId = StockLedgerStreamId.For(
                        command.WarehouseId, command.Location, line.SKU);

                    if (!stockEventsByStream.ContainsKey(stockStreamId))
                        stockEventsByStream[stockStreamId] = new List<object>();

                    stockEventsByStream[stockStreamId].Add(stockMovedEvent);

                    _logger.LogDebug(
                        "Queued StockMoved: {Qty} x {SKU} SUPPLIER → {Location} (HU {HuId})",
                        line.Quantity, line.SKU, command.Location, huId);
                }

                foreach (var (streamId, events) in stockEventsByStream)
                {
                    var expectedVersion = stockStreamVersions[streamId];
                    session.Events.Append(streamId, expectedVersion, events.ToArray());
                }

                // ── Step 3: Emit HandlingUnitSealed ──────────────────────────
                var sealedEvent = new HandlingUnitSealedEvent
                {
                    HuId = huId,
                    SealedAt = now,
                    Timestamp = now
                };

                session.Events.Append(huStreamId, sealedEvent);

                _logger.LogDebug("Queued HandlingUnitSealed: HU {HuId}", huId);

                // ── Step 4: Commit atomically ────────────────────────────────
                await session.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "ReceiveGoods transaction committed: HU {HuId} / LPN {LPN}, {LineCount} line(s) (attempt {Attempt})",
                    huId, lpn, command.Lines.Count, attempt);

                return Result<Guid>.Ok(huId);
            }
            catch (EventStreamUnexpectedMaxEventIdException ex) when (attempt < MaxAttempts)
            {
                _logger.LogWarning(
                    ex,
                    "Concurrency conflict on attempt {Attempt}/{MaxAttempts} for ReceiveGoods HU {HuId}. Retrying.",
                    attempt, MaxAttempts, huId);

                await Task.Delay(100 * attempt, ct);
            }
            catch (EventStreamUnexpectedMaxEventIdException)
            {
                _logger.LogError(
                    "Concurrency conflict on final attempt for ReceiveGoods HU {HuId}",
                    huId);
                return Result<Guid>.Fail(DomainErrorCodes.ConcurrencyConflict);
            }
            catch (DomainException ex)
            {
                // Domain rule violation (e.g., qty <= 0). Log detail, return safe code.
                _logger.LogWarning("ReceiveGoods domain validation failed: {Error}", ex.Message);
                return Result<Guid>.Fail(DomainErrorCodes.ReceiveGoodsFailed);
            }
            catch (Exception ex)
            {
                // Unexpected failure — never leak ex.Message to caller
                _logger.LogError(ex, "ReceiveGoods transaction failed for HU {HuId}", huId);
                return Result<Guid>.Fail(DomainErrorCodes.ReceiveGoodsFailed);
            }
        }

        // Should never reach here, but satisfy the compiler
        return Result<Guid>.Fail(DomainErrorCodes.ReceiveGoodsFailed);
    }

    private static string GenerateLPN()
    {
        return $"HU-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
    }
}
