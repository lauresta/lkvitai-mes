using MassTransit;

namespace LKvitai.MES.Sagas;

/// <summary>
/// PickStock saga per blueprint [MITIGATION V-3]
/// Two-step saga: StockMovement → Reservation consumption
/// HU projection updates asynchronously (no wait)
/// </summary>
public class PickStockSaga : MassTransitStateMachine<PickStockSagaState>
{
    // Saga placeholder - implementation per blueprint to be added
    // Step 1: Record StockMovement via StockLedger
    // Step 2: Consume Reservation
    // NO projection wait (removed per V-3 mitigation)
}

public class PickStockSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;
    public Guid ReservationId { get; set; }
    public Guid HandlingUnitId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
}
