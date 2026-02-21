using LKvitai.MES.Contracts.ReadModels;

namespace LKvitai.MES.Modules.Warehouse.Application.Ports;

public interface IReservationReadModelQueryService
{
    Task<int> CountReservationsAsync(string? normalizedStatus, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReservationSummaryView>> GetReservationPageAsync(
        string? normalizedStatus,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, HandlingUnitView>> GetHandlingUnitsAsync(
        IReadOnlyCollection<Guid> huIds,
        CancellationToken cancellationToken);
}
