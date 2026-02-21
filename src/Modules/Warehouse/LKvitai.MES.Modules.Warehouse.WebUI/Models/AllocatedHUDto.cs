namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public record AllocatedHUDto
{
    public Guid HuId { get; init; }
    public string LPN { get; init; } = string.Empty;
    public decimal Qty { get; init; }
}
