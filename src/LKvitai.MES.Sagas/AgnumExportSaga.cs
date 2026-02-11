using LKvitai.MES.Contracts.Messages;
using MassTransit;

namespace LKvitai.MES.Sagas;

public sealed class AgnumExportSaga : MassTransitStateMachine<AgnumExportSagaState>
{
    public State Processing { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State Failed { get; private set; } = null!;

    public Event<StartAgnumExport> Started { get; private set; } = null!;
    public Event<AgnumExportSucceeded> Succeeded { get; private set; } = null!;
    public Event<AgnumExportFailed> FailedEvent { get; private set; } = null!;

    public AgnumExportSaga()
    {
        InstanceState(x => x.CurrentState);

        Event(() => Started, x => x.CorrelateById(context => context.Message.CorrelationId));
        Event(() => Succeeded, x => x.CorrelateById(context => context.Message.CorrelationId));
        Event(() => FailedEvent, x => x.CorrelateById(context => context.Message.CorrelationId));

        Initially(
            When(Started)
                .Then(context =>
                {
                    context.Saga.Trigger = context.Message.Trigger;
                    context.Saga.RetryCount = context.Message.RetryCount;
                    context.Saga.StartedAt = DateTimeOffset.UtcNow;
                })
                .TransitionTo(Processing));

        During(Processing,
            When(Succeeded)
                .Then(context =>
                {
                    context.Saga.ExportNumber = context.Message.ExportNumber;
                    context.Saga.RowCount = context.Message.RowCount;
                    context.Saga.FilePath = context.Message.FilePath;
                    context.Saga.CompletedAt = DateTimeOffset.UtcNow;
                })
                .TransitionTo(Completed)
                .Finalize(),

            When(FailedEvent)
                .Then(context =>
                {
                    context.Saga.ExportNumber = context.Message.ExportNumber;
                    context.Saga.LastError = context.Message.ErrorMessage;
                    context.Saga.RetryCount = context.Message.RetryCount;
                })
                .IfElse(
                    context => context.Message.RetryCount >= 3,
                    binder => binder
                        .Then(context => context.Saga.CompletedAt = DateTimeOffset.UtcNow)
                        .TransitionTo(Failed)
                        .Finalize(),
                    binder => binder.TransitionTo(Processing)));

        SetCompletedWhenFinalized();
    }
}

public sealed class AgnumExportSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;

    public string Trigger { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public string ExportNumber { get; set; } = string.Empty;
    public int RowCount { get; set; }
    public string? FilePath { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
