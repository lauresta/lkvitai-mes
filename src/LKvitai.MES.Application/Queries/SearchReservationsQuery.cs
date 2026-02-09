using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Application.Queries;

public record SearchReservationsQuery : ICommand<PagedResult<ReservationDto>>
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    public string? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public record PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public record ReservationDto
{
    public Guid ReservationId { get; init; }
    public string Purpose { get; init; } = string.Empty;
    public int Priority { get; init; }
    public string Status { get; init; } = string.Empty;
    public string LockType { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public IReadOnlyList<ReservationLineDto> Lines { get; init; } = Array.Empty<ReservationLineDto>();
}

public record ReservationLineDto
{
    public string SKU { get; init; } = string.Empty;
    public decimal RequestedQty { get; init; }
    public decimal AllocatedQty { get; init; }
    public string Location { get; init; } = string.Empty;
    public string WarehouseId { get; init; } = string.Empty;
    public IReadOnlyList<AllocatedHUDto> AllocatedHUs { get; init; } = Array.Empty<AllocatedHUDto>();
}

public record AllocatedHUDto
{
    public Guid HuId { get; init; }
    public string LPN { get; init; } = string.Empty;
    public decimal Qty { get; init; }
}
