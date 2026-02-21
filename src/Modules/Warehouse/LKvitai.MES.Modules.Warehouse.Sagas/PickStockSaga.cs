using LKvitai.MES.Modules.Warehouse.Application.Orchestration;
using LKvitai.MES.Contracts.Messages;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Modules.Warehouse.Sagas;

/// <summary>
/// PickStock saga per blueprint [MITIGATION V-3]
///
/// Handles durable retry of reservation consumption when the in-process
/// consumption fails after StockMovement is committed.
///
/// State machine:
///   Initial → ConsumingReservation (on ConsumePickReservationDeferred)
///   ConsumingReservation → Completed (on PickReservationConsumed)
///   ConsumingReservation → ConsumingReservation (on RetryConsumeReservation — bounded retry)
///   ConsumingReservation → Failed (on max retries → PickStockFailedPermanentlyEvent)
///
/// Durable retry: Uses MassTransit Schedule (survives process restart).
/// DLQ: Publishes PickStockFailedPermanentlyEvent for supervisor alert.
/// NO Task.Delay — all retry is via MassTransit scheduling.
/// </summary>
public class PickStockSaga : MassTransitStateMachine<PickStockSagaState>
{
    private readonly ILogger<PickStockSaga> _logger;

    /// <summary>Maximum number of durable retry attempts.</summary>
    public const int MaxRetryAttempts = 3;

    // ── States ──────────────────────────────────────────────────────
    public State ConsumingReservation { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Failed { get; private set; } = null!;

    // ── Events ─────────────────────────────────────────────────────
    public Event<ConsumePickReservationDeferred> ConsumeDeferred { get; private set; } = null!;
    public Event<PickReservationConsumed> ReservationConsumed { get; private set; } = null!;
    public Event<PickReservationConsumptionFailed> ConsumptionFailed { get; private set; } = null!;

    // ── Schedule (durable retry, no Task.Delay) ────────────────────
    public Schedule<PickStockSagaState, RetryConsumeReservation> RetrySchedule { get; private set; } = null!;

    public PickStockSaga(ILogger<PickStockSaga> logger)
    {
        _logger = logger;

        InstanceState(x => x.CurrentState);

        // ── Configure events ───────────────────────────────────────────
        Event(() => ConsumeDeferred, x =>
            x.CorrelateById(ctx => ctx.Message.CorrelationId));

        Event(() => ReservationConsumed, x =>
            x.CorrelateById(ctx => ctx.Message.CorrelationId));

        Event(() => ConsumptionFailed, x =>
            x.CorrelateById(ctx => ctx.Message.CorrelationId));

        // ── Configure durable retry schedule ───────────────────────────
        Schedule(() => RetrySchedule, x => x.RetryScheduleTokenId, s =>
        {
            s.Delay = TimeSpan.FromSeconds(5); // default delay, overridden per attempt
            s.Received = e => e.CorrelateById(ctx => ctx.Message.CorrelationId);
        });

        // ── State machine transitions ──────────────────────────────────

        // Entry: Receive deferred consumption request
        Initially(
            When(ConsumeDeferred)
                .Then(ctx =>
                {
                    ctx.Saga.ReservationId = ctx.Message.ReservationId;
                    ctx.Saga.Quantity = ctx.Message.Quantity;
                    ctx.Saga.MovementId = ctx.Message.MovementId;
                    ctx.Saga.WarehouseId = ctx.Message.WarehouseId;
                    ctx.Saga.SKU = ctx.Message.SKU;
                    ctx.Saga.FromLocation = ctx.Message.FromLocation;
                    ctx.Saga.RetryCount = 0;

                    _logger.LogInformation(
                        "PickStockSaga started: Reservation {ReservationId}, Movement {MovementId}",
                        ctx.Message.ReservationId, ctx.Message.MovementId);
                })
                .Activity(x => x.OfType<ConsumeReservationActivity>())
                .TransitionTo(ConsumingReservation)
        );

        // ConsumingReservation: handle success, failure, and scheduled retry
        During(ConsumingReservation,
            When(ReservationConsumed)
                .Then(ctx =>
                {
                    _logger.LogInformation(
                        "PickStockSaga completed: Reservation {ReservationId}",
                        ctx.Saga.ReservationId);
                })
                .TransitionTo(Completed)
                .Finalize(),

            When(ConsumptionFailed)
                .IfElse(
                    ctx => ctx.Saga.RetryCount < MaxRetryAttempts,
                    // Retry with exponential backoff via MassTransit schedule (durable!)
                    retry => retry
                        .Then(ctx =>
                        {
                            ctx.Saga.RetryCount++;
                            ctx.Saga.LastError = ctx.Message.Reason;

                            _logger.LogWarning(
                                "PickStockSaga retry {RetryCount}/{Max} for Reservation {ReservationId}: {Reason}",
                                ctx.Saga.RetryCount, MaxRetryAttempts,
                                ctx.Saga.ReservationId, ctx.Message.Reason);
                        })
                        .Schedule(
                            RetrySchedule,
                            ctx => ctx.Init<RetryConsumeReservation>(new
                            {
                                CorrelationId = ctx.Saga.CorrelationId
                            }),
                            ctx => ComputeRetryDelay(ctx.Saga.RetryCount)),
                    // Permanent failure — DLQ + supervisor alert
                    fail => fail
                        .Then(ctx =>
                        {
                            ctx.Saga.LastError = ctx.Message.Reason;

                            _logger.LogError(
                                "PickStockSaga PERMANENTLY FAILED for Reservation {ReservationId} " +
                                "after {MaxRetries} retries. Movement {MovementId} is committed. " +
                                "Reservation NOT consumed. Supervisor intervention required.",
                                ctx.Saga.ReservationId, MaxRetryAttempts, ctx.Saga.MovementId);
                        })
                        .Publish(ctx => new PickStockFailedPermanentlyEvent
                        {
                            CorrelationId = ctx.Saga.CorrelationId,
                            ReservationId = ctx.Saga.ReservationId,
                            MovementId = ctx.Saga.MovementId,
                            Reason = ctx.Saga.LastError ?? "Max retries exhausted",
                            FailedAt = DateTime.UtcNow
                        })
                        .TransitionTo(Failed)
                        .Finalize()
                ),

            // Scheduled retry fires — attempt consumption again
            When(RetrySchedule!.Received)
                .Activity(x => x.OfType<ConsumeReservationActivity>())
        );

        // Clean up completed/failed saga instances
        SetCompletedWhenFinalized();
    }

    private static TimeSpan ComputeRetryDelay(int retryCount)
    {
        // Exponential backoff: 5s, 15s, 45s
        return TimeSpan.FromSeconds(5 * Math.Pow(3, retryCount - 1));
    }
}

/// <summary>
/// MassTransit activity that attempts reservation consumption via IPickStockOrchestration.
/// Publishes PickReservationConsumed or PickReservationConsumptionFailed based on result.
/// </summary>
public class ConsumeReservationActivity :
    IStateMachineActivity<PickStockSagaState, ConsumePickReservationDeferred>,
    IStateMachineActivity<PickStockSagaState, RetryConsumeReservation>
{
    private readonly IPickStockOrchestration _orchestration;
    private readonly ILogger<ConsumeReservationActivity> _logger;

    public ConsumeReservationActivity(
        IPickStockOrchestration orchestration,
        ILogger<ConsumeReservationActivity> logger)
    {
        _orchestration = orchestration;
        _logger = logger;
    }

    // ── For initial ConsumePickReservationDeferred ───────────────────
    public async Task Execute(
        BehaviorContext<PickStockSagaState, ConsumePickReservationDeferred> context,
        IBehavior<PickStockSagaState, ConsumePickReservationDeferred> next)
    {
        await AttemptConsumption(context);
        await next.Execute(context);
    }

    public async Task Faulted<TException>(
        BehaviorExceptionContext<PickStockSagaState, ConsumePickReservationDeferred, TException> context,
        IBehavior<PickStockSagaState, ConsumePickReservationDeferred> next)
        where TException : Exception
    {
        await next.Faulted(context);
    }

    // ── For scheduled RetryConsumeReservation ───────────────────────
    public async Task Execute(
        BehaviorContext<PickStockSagaState, RetryConsumeReservation> context,
        IBehavior<PickStockSagaState, RetryConsumeReservation> next)
    {
        await AttemptConsumption(context);
        await next.Execute(context);
    }

    public async Task Faulted<TException>(
        BehaviorExceptionContext<PickStockSagaState, RetryConsumeReservation, TException> context,
        IBehavior<PickStockSagaState, RetryConsumeReservation> next)
        where TException : Exception
    {
        await next.Faulted(context);
    }

    // ── Shared consumption logic ────────────────────────────────────
    private async Task AttemptConsumption<T>(BehaviorContext<PickStockSagaState, T> context)
        where T : class
    {
        var saga = context.Saga;

        try
        {
            var result = await _orchestration.ConsumeReservationAsync(
                saga.ReservationId, saga.Quantity, context.CancellationToken);

            if (result.IsSuccess)
            {
                await context.Publish(new PickReservationConsumed
                {
                    CorrelationId = saga.CorrelationId,
                    ReservationId = saga.ReservationId
                });
            }
            else
            {
                await context.Publish(new PickReservationConsumptionFailed
                {
                    CorrelationId = saga.CorrelationId,
                    ReservationId = saga.ReservationId,
                    Reason = result.Error,
                    RetryCount = saga.RetryCount
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "ConsumeReservation activity exception for Reservation {ReservationId}",
                saga.ReservationId);

            await context.Publish(new PickReservationConsumptionFailed
            {
                CorrelationId = saga.CorrelationId,
                ReservationId = saga.ReservationId,
                Reason = ex.GetType().Name,
                RetryCount = saga.RetryCount
            });
        }
    }

    public void Probe(ProbeContext context) => context.CreateScope("consume-reservation");
    public void Accept(StateMachineVisitor visitor) => visitor.Visit(this);
}

/// <summary>
/// Saga state persisted by MassTransit + Marten.
/// </summary>
public class PickStockSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;

    // ── Saga data ──────────────────────────────────────────────────
    public Guid ReservationId { get; set; }
    public Guid MovementId { get; set; }
    public string WarehouseId { get; set; } = string.Empty;
    public string SKU { get; set; } = string.Empty;
    public string FromLocation { get; set; } = string.Empty;
    public decimal Quantity { get; set; }

    // ── Retry tracking ─────────────────────────────────────────────
    public int RetryCount { get; set; }
    public string? LastError { get; set; }

    /// <summary>
    /// Token for the durable retry schedule.
    /// MassTransit uses this to track scheduled messages.
    /// </summary>
    public Guid? RetryScheduleTokenId { get; set; }
}
