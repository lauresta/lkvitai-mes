using LKvitai.MES.Application.Ports;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Domain.Aggregates;
using LKvitai.MES.SharedKernel;
using Marten;
using MediatR;

namespace LKvitai.MES.Application.Queries;

public class SearchReservationsQueryHandler
    : IRequestHandler<SearchReservationsQuery, Result<PagedResult<ReservationDto>>>
{
    private static readonly string AllocatedStatus = ReservationStatus.ALLOCATED.ToString();
    private static readonly string PickingStatus = ReservationStatus.PICKING.ToString();

    private readonly IDocumentStore _store;
    private readonly IReservationRepository _reservationRepository;

    public SearchReservationsQueryHandler(
        IDocumentStore store,
        IReservationRepository reservationRepository)
    {
        _store = store;
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

        await using var querySession = _store.QuerySession();
        IQueryable<ReservationSummaryView> summaryQuery = querySession.Query<ReservationSummaryView>();

        if (normalizedStatus is null)
        {
            summaryQuery = summaryQuery.Where(x => x.Status == AllocatedStatus || x.Status == PickingStatus);
        }
        else
        {
            summaryQuery = summaryQuery.Where(x => x.Status == normalizedStatus);
        }

        var totalCount = await summaryQuery.CountAsync(cancellationToken);
        var skip = (request.Page - 1) * request.PageSize;

        var summaries = await summaryQuery
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.ReservationId)
            .Skip(skip)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

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
            .ToArray();

        var huLookup = huIds.Length == 0
            ? new Dictionary<Guid, HandlingUnitView>()
            : (await querySession.Query<HandlingUnitView>()
                .Where(x => huIds.Contains(x.HuId))
                .ToListAsync(cancellationToken))
                .GroupBy(x => x.HuId)
                .ToDictionary(x => x.Key, x => x.First());

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
