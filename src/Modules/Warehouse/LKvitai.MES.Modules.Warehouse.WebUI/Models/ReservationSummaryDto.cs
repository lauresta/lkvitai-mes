namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public record ReservationSummaryDto
{
    public int Allocated { get; init; }
    public int Picking { get; init; }
    public int Consumed { get; init; }
}
