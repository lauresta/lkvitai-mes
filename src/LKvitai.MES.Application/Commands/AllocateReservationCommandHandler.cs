using LKvitai.MES.Application.Orchestration;
using LKvitai.MES.SharedKernel;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Application.Commands;

/// <summary>
/// Handles AllocateReservationCommand by delegating to IAllocateReservationOrchestration.
/// </summary>
public class AllocateReservationCommandHandler : IRequestHandler<AllocateReservationCommand, Result>
{
    private readonly IAllocateReservationOrchestration _orchestration;
    private readonly ILogger<AllocateReservationCommandHandler> _logger;

    public AllocateReservationCommandHandler(
        IAllocateReservationOrchestration orchestration,
        ILogger<AllocateReservationCommandHandler> logger)
    {
        _orchestration = orchestration;
        _logger = logger;
    }

    public async Task<Result> Handle(AllocateReservationCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "AllocateReservation command received for Reservation {ReservationId} in Warehouse {WarehouseId}",
            request.ReservationId, request.WarehouseId);

        var result = await _orchestration.AllocateAsync(
            request.ReservationId, request.WarehouseId, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "AllocateReservation failed for Reservation {ReservationId}: {Error}",
                request.ReservationId, result.Error);
        }

        return result;
    }
}
