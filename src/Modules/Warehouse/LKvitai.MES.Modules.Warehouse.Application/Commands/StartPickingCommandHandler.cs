using LKvitai.MES.Modules.Warehouse.Application.Orchestration;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Modules.Warehouse.Application.Commands;

/// <summary>
/// StartPicking command handler — delegates to <see cref="IStartPickingOrchestration"/>
/// for atomic HARD lock acquisition.
///
/// [MITIGATION R-3] Implements atomic balance re-validation and conflict detection.
///
/// Workflow:
///   1. Delegates to orchestration service (infrastructure concern)
///   2. Orchestration handles: load reservation, advisory lock, re-validate balance,
///      check hard lock conflicts, append PickingStarted event, inline projection update
///   3. Handler returns the orchestration result
///
/// Retry logic is handled internally by the orchestration service.
/// </summary>
public class StartPickingCommandHandler : IRequestHandler<StartPickingCommand, Result>
{
    private readonly IStartPickingOrchestration _orchestration;
    private readonly ILogger<StartPickingCommandHandler> _logger;

    public StartPickingCommandHandler(
        IStartPickingOrchestration orchestration,
        ILogger<StartPickingCommandHandler> logger)
    {
        _orchestration = orchestration;
        _logger = logger;
    }

    public async Task<Result> Handle(StartPickingCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "StartPicking command received for Reservation {ReservationId} by Operator {OperatorId}",
            request.ReservationId, request.OperatorId);

        var result = await _orchestration.StartPickingAsync(
            request.ReservationId, request.OperatorId, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "StartPicking failed for Reservation {ReservationId}: {Error}",
                request.ReservationId, result.Error);
        }

        return result;
    }
}
