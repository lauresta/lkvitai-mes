namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public record PickRequestDto
{
    public Guid ReservationId { get; init; }
    public Guid HuId { get; init; }
    public string Sku { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
}
