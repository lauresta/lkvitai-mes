namespace LKvitai.MES.WebUI.Models;

public record ProjectionHealthDto
{
    public double LocationBalanceLag { get; init; }
    public double AvailableStockLag { get; init; }
    public DateTime? LastRebuildLB { get; init; }
    public DateTime? LastRebuildAS { get; init; }
}
