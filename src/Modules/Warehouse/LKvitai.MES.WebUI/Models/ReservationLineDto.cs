namespace LKvitai.MES.WebUI.Models;

public record ReservationLineDto
{
    public string SKU { get; init; } = string.Empty;
    public decimal RequestedQty { get; init; }
    public decimal AllocatedQty { get; init; }
    public string Location { get; init; } = string.Empty;
    public string WarehouseId { get; init; } = string.Empty;
    public IReadOnlyList<AllocatedHUDto> AllocatedHUs { get; init; } = Array.Empty<AllocatedHUDto>();
}
