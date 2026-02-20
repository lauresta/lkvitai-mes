using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.SharedKernel;
using MediatR;

namespace LKvitai.MES.Modules.Warehouse.Application.Queries;

public class SearchReservationsQueryHandler
    : IRequestHandler<SearchReservationsQuery, Result<PagedResult<ReservationDto>>>
{
    private readonly IReservationReadModelQueryService _readModelQueryService;
    private readonly IReservationRepository _reservationRepository;

    public SearchReservationsQueryHandler(
        IReservationReadModelQueryService readModelQueryService,
        IReservationRepository reservationRepository)
    {
        _readModelQueryService = readModelQueryService;
        _reservationRepository = reservationRepository;
    }

    public async Task<Result<PagedResult<ReservationDto>>> Handle(
        SearchReservationsQuery request,
        CancellationToken cancellationToken)
    {
        if (request.Page < 1)
        {
            return Result<PagedResult<ReservationDto>>.Fail(
                DomainErrorCodes.ValidationError,
                "Page must be greater than or equal to 1.");
        }

        if (request.PageSize < 1 || request.PageSize > 100)
        {
            return Result<PagedResult<ReservationDto>>.Fail(
                DomainErrorCodes.ValidationError,
                "PageSize must be between 1 and 100.");
        }

        var normalizedStatus = string.IsNullOrWhiteSpace(request.Status)
            ? null
            : request.Status.Trim().ToUpperInvariant();

        if (normalizedStatus is not null &&
            normalizedStatus is not (nameof(ReservationStatus.ALLOCATED) or nameof(ReservationStatus.PICKING)))
        {
            return Result<PagedResult<ReservationDto>>.Fail(
                DomainErrorCodes.ValidationError,
                "Status must be one of: ALLOCATED, PICKING.");
        }

        var totalCount = await _readModelQueryService.CountReservationsAsync(normalizedStatus, cancellationToken);
        var skip = (request.Page - 1) * request.PageSize;
        var summaries = await _readModelQueryService.GetReservationPageAsync(
            normalizedStatus,
            skip,
            request.PageSize,
            cancellationToken);

        if (summaries.Count == 0)
        {
            return Result<PagedResult<ReservationDto>>.Ok(new PagedResult<ReservationDto>
            {
                Items = Array.Empty<ReservationDto>(),
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize
            });
        }

        var loadTasks = summaries
            .Select(summary => _reservationRepository.LoadAsync(summary.ReservationId, cancellationToken))
            .ToList();
        var loadedReservations = await Task.WhenAll(loadTasks);

        var reservationById = new Dictionary<Guid, Reservation>();
        for (var index = 0; index < summaries.Count; index++)
        {
            var reservation = loadedReservations[index].Reservation;
            if (reservation is not null)
            {
                reservationById[summaries[index].ReservationId] = reservation;
            }
        }

        var huIds = reservationById.Values
            .SelectMany(x => x.Lines)
            .SelectMany(x => x.AllocatedHUs)
            .Distinct()
            .ToList();

        var huLookup = huIds.Count == 0
            ? new Dictionary<Guid, HandlingUnitView>()
            : new Dictionary<Guid, HandlingUnitView>(
                await _readModelQueryService.GetHandlingUnitsAsync(huIds, cancellationToken));

        var items = new List<ReservationDto>(summaries.Count);

        foreach (var summary in summaries)
        {
            if (!reservationById.TryGetValue(summary.ReservationId, out var reservation))
            {
                continue;
            }

            var lines = reservation.Lines
                .Select(line => new ReservationLineDto
                {
                    SKU = line.SKU,
                    RequestedQty = line.RequestedQuantity,
                    AllocatedQty = line.AllocatedQuantity,
                    Location = line.Location,
                    WarehouseId = line.WarehouseId,
                    AllocatedHUs = line.AllocatedHUs
                        .Select(huId => MapAllocatedHu(
                            huId,
                            line.SKU,
                            line.AllocatedQuantity,
                            line.AllocatedHUs.Count,
                            huLookup))
                        .ToList()
                })
                .ToList();

            items.Add(new ReservationDto
            {
                ReservationId = summary.ReservationId,
                Purpose = summary.Purpose,
                Priority = summary.Priority,
                Status = summary.Status,
                LockType = summary.LockType,
                CreatedAt = summary.CreatedAt,
                Lines = lines
            });
        }

        return Result<PagedResult<ReservationDto>>.Ok(new PagedResult<ReservationDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }

    private static AllocatedHUDto MapAllocatedHu(
        Guid huId,
        string sku,
        decimal allocatedQty,
        int allocatedHuCount,
        IReadOnlyDictionary<Guid, HandlingUnitView> huLookup)
    {
        if (huLookup.TryGetValue(huId, out var hu))
        {
            var quantity = hu.Lines
                .FirstOrDefault(line => string.Equals(line.SKU, sku, StringComparison.OrdinalIgnoreCase))
                ?.Quantity ?? 0m;

            if (quantity <= 0m && allocatedHuCount == 1)
            {
                quantity = allocatedQty;
            }

            return new AllocatedHUDto
            {
                HuId = huId,
                LPN = hu.LPN,
                Qty = quantity
            };
        }

        return new AllocatedHUDto
        {
            HuId = huId,
            LPN = string.Empty,
            Qty = allocatedHuCount == 1 ? allocatedQty : 0m
        };
    }
}
