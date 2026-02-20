using MassTransit;

namespace LKvitai.MES.Sagas;

/// <summary>
/// ReceiveGoods saga per blueprint
/// Orchestrates goods receipt workflow from supplier
/// </summary>
public class ReceiveGoodsSaga : MassTransitStateMachine<ReceiveGoodsSagaState>
{
    // Saga placeholder - implementation per blueprint to be added
}

public class ReceiveGoodsSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;
}
