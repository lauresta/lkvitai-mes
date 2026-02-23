namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public sealed record LocationBalanceItemDto
{
    public int? LocationId { get; init; }
    public string LocationCode { get; init; } = string.Empty;
    public int ItemCount { get; init; }
    public decimal TotalWeight { get; init; }
    public decimal TotalVolume { get; init; }
    public decimal? MaxWeight { get; init; }
    public decimal? MaxVolume { get; init; }
    public decimal? UtilizationWeight { get; init; }
    public decimal? UtilizationVolume { get; init; }
    public string Status { get; init; } = string.Empty;
}
