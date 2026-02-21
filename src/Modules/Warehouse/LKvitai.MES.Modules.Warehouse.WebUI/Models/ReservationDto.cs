namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

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
