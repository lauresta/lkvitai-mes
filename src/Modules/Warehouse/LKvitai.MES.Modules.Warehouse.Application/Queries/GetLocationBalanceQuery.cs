using LKvitai.MES.BuildingBlocks.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Application.Queries;

/// <summary>
/// Query to get location balance for a specific (warehouseId, location, SKU)
/// Used by StartPicking and allocation logic
/// </summary>
public record GetLocationBalanceQuery : ICommand<LocationBalanceDto?>
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }
    
    public string WarehouseId { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string SKU { get; init; } = string.Empty;
}

/// <summary>
/// Location balance DTO
/// </summary>
public record LocationBalanceDto
{
    public string WarehouseId { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string SKU { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public DateTime LastUpdated { get; init; }
}
